using System.Configuration;
using System.Data;
using System.Windows;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;



namespace ShimejiLane;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    int id = -1;
    bool inAir = false;
    bool isDragged = false;
    bool isClimbing = false;
    Point grabPoint;
    Vector velocity;
    float gravity = 1500;
    float mass = 1;
    float airFriction = 1.3f;
    float throwSpeed = 300; 
    float carriedTurnSpeed = 100;
    // Transparent space inside the climbing sprites; the window is pushed past the screen edge
    // by this much so the body touches the wall/ceiling instead of hanging in the air
    // Sprites are bottom-centred in the 140x130 window, so a 128px-wide frame starts 6px in
    float wallInset = 64;    // shime12-14: 6px centring + ~20px before the reaching hand
    float ceilingInset = 55; // shime23-25: empty rows above the raised hands and feet
    enum Action{
        MoveLeft,
        MoveRight,
        Stand,

        Falling,
        Flying,
        Landing,
        ClimbingUpLeft,
        ClimbingUpRight,
        ClimbingTopRight,
        ClimbingTopLeft,
        ClimbingDownLeft,
        ClimbingDownRight,
        ClimbingIdleLeft,
        ClimbingIdleRight,
        ClimbingIdleTop,
        CarriedLeft,
        CarriedRight,
        Carried,
        // CrawlingRight,
        // CrawlingLeft,
        // JumpLeft,
        // JumpRight,
        Sit,


    }
    Action selectedAction = Action.Stand;
    float actionTime = 0;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TimeSpan lastTime;
    int HZ = Display.GetRefreshRate();
    BitmapImage[] sprites = new BitmapImage[47];

    Random random = new Random();
    Rect screen;
    TrayIcon tray;
    
    public MainWindow()
    {
        InitializeComponent();
        for (int i = 1; i <= 46; i++)
        {
            sprites[i] = new BitmapImage(new Uri($"pack://application:,,,/img/shime{i}.png"));
        }
        SetSprite(1);
        CompositionTarget.Rendering += OnRendering;
        Loaded += MainWindow_Loaded;
        screen = SystemParameters.WorkArea;
        tray = new TrayIcon(this);
        Closed += (_, _) => tray.Dispose();

    }
    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = clock.Elapsed;
        double deltaTime = (now - lastTime).TotalSeconds;
        lastTime = now;

        Update((float)deltaTime);
    }
    private void Update(float deltaTime)
    {
        if (isDragged)
        {
            Drag(deltaTime);
            DoAction(deltaTime);
            return;
        }
        if (selectedAction == Action.Falling || selectedAction == Action.Flying)
        {
            FreeFall(deltaTime);
            DoAction(deltaTime);
            return;
        }
        if (isClimbing && actionTime<= 0)
        {
            var rng = new Random();
            if(selectedAction == Action.ClimbingTopRight || selectedAction == Action.ClimbingTopLeft || selectedAction == Action.ClimbingIdleTop)
            {
                int random = rng.Next(4);
                if (random == 0)
                {
                    selectedAction = Action.ClimbingTopLeft;
                    Rotate(1);
                }
                else if (random == 1)
                {
                    selectedAction = Action.ClimbingTopRight;
                    Rotate(-1);
                }
                else if (random == 2)
                {
                    selectedAction = Action.ClimbingIdleTop;
                }
                else
                {
                    selectedAction = Action.Falling;
                    isClimbing = false;
                    Rotate(1);
                }
            }
            else
            {
                bool onLeftWall = selectedAction == Action.ClimbingUpLeft || selectedAction == Action.ClimbingDownLeft || selectedAction == Action.ClimbingIdleLeft;
                int random = rng.Next(4);
                if (random == 0)
                {
                    selectedAction = onLeftWall ? Action.ClimbingUpLeft : Action.ClimbingUpRight;
                }
                else if (random == 1)
                {
                    selectedAction = onLeftWall ? Action.ClimbingDownLeft : Action.ClimbingDownRight;
                }
                else if (random == 2)
                {
                    selectedAction = onLeftWall ? Action.ClimbingIdleLeft : Action.ClimbingIdleRight;
                }
                else
                {
                    selectedAction = Action.Falling;
                    isClimbing = false;
                    Rotate(1);
                }
            }
            actionTime = GetRandomTime();

        }
        if(actionTime<= 0 && !isDragged && !isClimbing)
        {
            actionTime = GetRandomTime();
            selectedAction = randomActions[random.Next(0, randomActions.Count)];
            RestartAnim();
        }
        actionTime -= (float)deltaTime;
        DoAction(deltaTime);
        

    }
    private int GetRandomTime()
    {
        return random.Next(5, 6);
    }
    private void Drag(float deltaTime)
    {
        // Place the grabbed point exactly under the cursor instead of nudging by an offset;
        // Mouse.GetPosition can be stale right after the window moves, which made it run away
        Point cursor = CursorOnScreen();
        Vector move = new Vector(cursor.X - grabPoint.X - Left, cursor.Y - grabPoint.Y - Top);
        Left += move.X;
        Top += move.Y;

        if (deltaTime > 0) velocity = velocity * 0.8 + move / deltaTime * 0.2;
        if (velocity.X < -carriedTurnSpeed) selectedAction = Action.CarriedLeft;
        else if (velocity.X > carriedTurnSpeed) selectedAction = Action.CarriedRight;
        else selectedAction = Action.Carried;
    }
    // Real cursor position on screen, converted to the same units as Left/Top (DPI-aware)
    private Point CursorOnScreen()
    {
        System.Drawing.Point pixels = System.Windows.Forms.Cursor.Position;
        Matrix fromDevice = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
        return fromDevice.Transform(new Point(pixels.X, pixels.Y));
    }
    private void Sprite_LostMouseCapture(object sender, MouseEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!isDragged) return;
        isDragged = false;
        Sprite.ReleaseMouseCapture();
        selectedAction = velocity.Length > throwSpeed ? Action.Flying : Action.Falling;
        TryGrabEdge(released: true);
    }
    // Grabs onto the ceiling or a side wall: when released while touching it,
    // or when flying into it. Picks between climbing and hanging still. Returns true if it grabbed.
    private bool TryGrabEdge(bool released)
    {
        // In the air it only grabs what it moves towards, so falling off a wall doesn't re-grab it
        bool hitTop = Top <= screen.Top && (released || velocity.Y < 0);
        bool hitLeft = Left <= screen.Left && (released || velocity.X < 0);
        bool hitRight = Left + Width >= screen.Right && (released || velocity.X > 0);
        if (!hitTop && !hitLeft && !hitRight)
        {
            return false;
        }

        if (hitTop)
        {
            Top = screen.Top - ceilingInset;
            Left = Math.Clamp(Left, screen.Left, screen.Right - Width);
            int choice = random.Next(3);
            if (choice == 0)
            {
                selectedAction = Action.ClimbingTopLeft;
            }
            else if (choice == 1)
            {
                selectedAction = Action.ClimbingTopRight;
            }
            else
            {
                selectedAction = Action.ClimbingIdleTop;
            }
        }
        else if (hitLeft)
        {
            Left = screen.Left - wallInset;
            selectedAction = random.Next(2) == 0 ? Action.ClimbingUpLeft : Action.ClimbingIdleLeft;
            Rotate(1);
        }
        else
        {
            Left = screen.Right - Width + wallInset;
            selectedAction = random.Next(2) == 0 ? Action.ClimbingUpRight : Action.ClimbingIdleRight;
            Rotate(-1);
        }
        isClimbing = true;
        velocity = new Vector();
        actionTime = GetRandomTime();
        return true;
    }
    private void FreeFall(float deltaTime)
    {
        velocity.Y = velocity.Y + gravity * deltaTime;
        
        if(Left <= screen.Left || Left + Width >= screen.Right)
        {
            velocity.X = 0;
        }
        else
        {
            velocity.X = velocity.X * Math.Pow(Math.E, -deltaTime * (airFriction / mass));
        }

        Left += velocity.X * deltaTime;
        Top += velocity.Y * deltaTime;

        if (Top + Height >= screen.Bottom)
        {
            Top = screen.Bottom - Height;
            Left = Math.Clamp(Left, screen.Left, screen.Right - Width); // e.g. fell off a wall inset
            velocity = new Vector();
            selectedAction = Action.Landing;
            actionTime = 5;
            return;
        }
        if (TryGrabEdge(released: false))
        {
            return;
        }
        if (Top <= screen.Top)
        {
            Top = screen.Top;
            velocity.Y = 0;
        }
        if (selectedAction == Action.Flying && velocity.Length <= throwSpeed)
        {
            selectedAction = Action.Falling;
        }
    }
    void SetSprite(int number)
    {
        // Draw at the image's pixel size; otherwise WPF scales by the file's DPI
        // (a 128px sprite saved at 144 DPI would be drawn only 85px big and float)
        Sprite.Source = sprites[number];
        Sprite.Width = sprites[number].PixelWidth;
        Sprite.Height = sprites[number].PixelHeight;
    }
    float speed = 50;
    float animTime = 0.25f;
    float curAnimTime = 0;
    int curFrame = 0;
    Action? animAction = null;
    bool animFinished = false;
    private void DoAction(float deltaTime)
    {
        switch (selectedAction)
        {
            case Action.Stand:
                PlayAnim(deltaTime);
                Rotate(1);
                break;
             case Action.Sit:
                PlayAnim(deltaTime);
                Rotate(1);
                break;
            case Action.MoveLeft:
                PlayAnim(deltaTime);
                Rotate(1);
                if(Left <= screen.Left)
                {
                    isClimbing = true;
                    Left = screen.Left - wallInset;
                    selectedAction = Action.ClimbingUpLeft;
                    actionTime = GetRandomTime();
                    Rotate(1);
                    break;
                }
                Left -= speed * deltaTime;
                break;
            case Action.MoveRight:
                PlayAnim(deltaTime);
                Rotate(-1);
                if(Left + Width >= screen.Right)
                {
                    isClimbing = true;
                    Left = screen.Right - Width + wallInset;
                    selectedAction = Action.ClimbingUpRight;
                    actionTime = GetRandomTime();
                    Rotate(-1);
                    break;
                }
                Left += speed * deltaTime;
                break;
            case Action.ClimbingUpLeft:
                Top -= speed * deltaTime;
                if(Top <= screen.Top)
                {
                    Top = screen.Top - ceilingInset;
                    Left = screen.Left;
                    selectedAction = Action.ClimbingTopRight;
                    actionTime = GetRandomTime();
                    Rotate(-1);
                }
                PlayAnim(deltaTime);
                break;
            case Action.ClimbingUpRight:
                Top -= speed * deltaTime;
                if(Top <= screen.Top)
                {
                    Top = screen.Top - ceilingInset;
                    Left = screen.Right - Width;
                    selectedAction = Action.ClimbingTopLeft;
                    actionTime = GetRandomTime();
                    Rotate(1);
                }
                PlayAnim(deltaTime);
                break;
            case Action.ClimbingTopRight:
                PlayAnim(deltaTime);
                Left += speed * deltaTime;
                Rotate(-1);
                if(Left + Width >= screen.Right)
                {
                    Left = screen.Right - Width + wallInset;
                    Top = screen.Top;
                    selectedAction = Action.ClimbingDownRight;
                    actionTime = GetRandomTime();
                }
                break;
            case Action.ClimbingTopLeft:
                PlayAnim(deltaTime);
                Left -= speed * deltaTime;
                Rotate(1);
                if(Left <= screen.Left)
                {
                    Left = screen.Left - wallInset;
                    Top = screen.Top;
                    selectedAction = Action.ClimbingDownLeft;
                    actionTime = GetRandomTime();
                }
                break;
            case Action.ClimbingIdleLeft:
                PlayAnim(deltaTime);
                Rotate(1);
                break;
            case Action.ClimbingIdleRight:
                PlayAnim(deltaTime);
                Rotate(-1);
                break;
            case Action.ClimbingIdleTop:
                PlayAnim(deltaTime);
                break;
            case Action.ClimbingDownLeft:
            case Action.ClimbingDownRight:
                PlayAnim(deltaTime);
                Top += speed * deltaTime;
                Rotate(selectedAction == Action.ClimbingDownLeft ? 1 : -1);
                if(Top + Height >= screen.Bottom)
                {
                    Top = screen.Bottom - Height;
                    Left = Math.Clamp(Left, screen.Left, screen.Right - Width); // step off the wall inset
                    isClimbing = false;
                    selectedAction = Action.Stand;
                    actionTime = 0;
                }
                break;
            case Action.Falling:
            case Action.Flying:
                PlayAnim(deltaTime);
                Rotate(velocity.X > 0 ? -1 : 1);
                break;
            case Action.Landing:
                if (PlayAnim(deltaTime, loop: false))
                {
                    actionTime = 0;
                }
                break;
            case Action.Carried:
            case Action.CarriedLeft:
                PlayAnim(deltaTime);
                Rotate(1);
                break;
            case Action.CarriedRight:
                PlayAnim(deltaTime);
                Rotate(1);
                break;
        }
    }
    // Restarts the current animation from its first frame on the next PlayAnim
    private void RestartAnim()
    {
        animAction = null;
    }
    // Plays animations[selectedAction] frame by frame.
    // loop = false stops on the last frame and returns true once it has been shown for animTime.
    private bool PlayAnim(float deltaTime, bool loop = true)
    {
        List<int> frames = animations[selectedAction];
        if (frames.Count == 0)
        {
            return true;
        }

        if (animAction != selectedAction)
        {
            animAction = selectedAction;
            curFrame = 0;
            curAnimTime = 0;
            animFinished = false;
        }

        curAnimTime += deltaTime;
        while (curAnimTime >= animTime && !animFinished)
        {
            curAnimTime -= animTime;
            if (curFrame < frames.Count - 1)
            {
                curFrame++;
            }
            else if (loop)
            {
                curFrame = 0;
            }
            else
            {
                animFinished = true;
            }
        }

        SetSprite(frames[curFrame]);
        return animFinished;
    }
    private List<Action> randomActions = new()
    {
        Action.MoveLeft,
        Action.MoveRight,
        Action.Stand,
        Action.Sit,
    };
    private Dictionary<Action, List<int>> animations = new Dictionary<Action, List<int>> (){
        {Action.MoveLeft, new List<int>{2,3}},
        {Action.MoveRight, new List<int>{2,3}},
        {Action.Stand, new List<int>{1}},
        {Action.Sit, new List<int>{11}},
        
        {Action.Falling, new List<int>{4}},
        {Action.Flying, new List<int>{22}},
        {Action.Landing, new List<int>{21}},//,20
        {Action.Carried, new List<int>{5}},//,6
        {Action.CarriedLeft, new List<int>{8}},//,10
        {Action.CarriedRight, new List<int>{7}}, //9,


        {Action.ClimbingUpLeft, new List<int>{12,13,14}},
        {Action.ClimbingUpRight, new List<int>{12,13,14}},
        {Action.ClimbingTopLeft, new List<int>{23,24,25}},
        {Action.ClimbingTopRight, new List<int>{23,24,25}},
        {Action.ClimbingDownLeft, new List<int>{14,13,12}},
        {Action.ClimbingDownRight, new List<int>{14,13,12}},
        {Action.ClimbingIdleLeft, new List<int>{12}},
        {Action.ClimbingIdleRight, new List<int>{12}},
        {Action.ClimbingIdleTop, new List<int>{23}},
        
    };
    private void Rotate(int leftRight)//left = 1, right = -1
    {
        Sprite.RenderTransformOrigin = new Point(0.5, 0.5);
        Sprite.RenderTransform = new ScaleTransform(leftRight, 1);
    }
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height;
    }

    private void Sprite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        actionTime = 0;
        isDragged = true;
        isClimbing = false;
        grabPoint = e.GetPosition(this);
        velocity = new Vector();
        Sprite.CaptureMouse();
        selectedAction = Action.Carried;

    }
    private void Sprite_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
