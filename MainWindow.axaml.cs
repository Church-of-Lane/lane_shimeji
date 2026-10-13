using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Java;
using DotNetEnv;
using System.Text.Json;



namespace ShimejiLane;

/// <summary>
/// Interaction logic for MainWindow.axaml
/// </summary>
public partial class MainWindow : Window
{
    // Window position in device-independent units, like WPF's Left/Top.
    // Written to Window.Position (desktop pixels) once per frame.
    double Left;
    double Top;
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
    Bitmap[] sprites = new Bitmap[47];

    Random random = new Random();
    Rect screen;
    TrayIcon tray;
    bool closed = false;
    IPointer? dragPointer;
    PixelPoint? pointerOnScreen; // fallback when the OS can't be asked for the cursor
    readonly ScaleTransform faceLeft = new ScaleTransform(1, 1);
    readonly ScaleTransform faceRight = new ScaleTransform(-1, 1);
    private readonly HttpClient _httpClient;
    private const string LANE_URL = "http://204.168.211.4:5081/";
    private string sessionID;

    public MainWindow()
    {
        InitializeComponent();
        for (int i = 1; i <= 46; i++)
        {
            sprites[i] = new Bitmap(AssetLoader.Open(new Uri($"avares://ShimejiLane/img/shime{i}.png")));
        }
        SetSprite(1);
        Sprite.PointerMoved += Sprite_PointerMoved;
        Opened += MainWindow_Opened;
        tray = new TrayIcon(this);
        Closed += (_, _) =>
        {
            closed = true;
            tray.Dispose();
        };
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(LANE_URL);

        Env.NoClobber().TraversePath().Load();
        string? laneKey = Environment.GetEnvironmentVariable("LANE_KEY");
        
        if (string.IsNullOrEmpty(laneKey))
        {
            return;
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", laneKey);


    }
    private bool laneConnected;
    // Toggled from the tray menu or the right-click menu; when off the pet only walks around
    public bool LaneEnabled { get; private set; } = true;
    // Lets the tray menu update its checkmark when the right-click menu changes it (and vice versa)
    public event System.Action? LaneEnabledChanged;
    public async void SetLaneEnabled(bool enabled)
    {
        LaneEnabled = enabled;
        LaneToggle.IsChecked = enabled;
        LaneEnabledChanged?.Invoke();
        if (!enabled)
        {
            Bubble.IsOpen = false;
            return;
        }
        if (!laneConnected)
        {
            try
            {
                laneConnected = await Connect();
            }
            catch (Exception ex)
            {
                Logger.Log("Connect failed: " + ex.Message);
            }
        }
    }
    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        laneConnected = await Connect();
        // Work area (screen minus taskbar / Dock / menu bar) in the same units as Left/Top
        Screen? display = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        screen = display != null ? display.WorkingArea.ToRect(DesktopScaling) : new Rect(0, 0, 1280, 720);
        Left = screen.Left + (screen.Width - Width) / 2;
        Top = screen.Bottom - Height;
        ApplyPosition();
        lastTime = clock.Elapsed;
        RequestAnimationFrame(OnFrame);
        
    }
    private float screenCount = 8f;
    private async Task<bool> Connect()
    {
        var response = await _httpClient.PostAsJsonAsync("/v1/sessions", new
        {
            key = "shimeji",
            displayName = "ShimejiLane",
            direct = true
        });

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        else return false;
    }
    private async void WorkWithScreen()
    {
        try
        {
            string base64 = Convert.ToBase64String(await ScreenCapture.DoScreenShot());
            var response = await _httpClient.PostAsJsonAsync("/v1/sessions/shimeji/messages", new
            {
                images = new[] { new { data = base64, mediaType = "image/jpeg" } }
            });

            string body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
    
            string messageId = doc.RootElement.GetProperty("messageId").GetString();

            string result = "";
            string historyBody = "";
            for (int attempt = 0; attempt < 15 && result == ""; attempt++)
            {
                await Task.Delay(2000);
                historyBody = await _httpClient.GetStringAsync("/v1/sessions/shimeji/messages");
                using var historyDoc = JsonDocument.Parse(historyBody);
                JsonElement messages = historyDoc.RootElement.GetProperty("messages");

                bool foundMine = false;
                foreach (JsonElement msg in messages.EnumerateArray())
                {
                    string? id = msg.GetProperty("id").GetString();
                    string? role = msg.GetProperty("role").GetString();
                    if (id == messageId)
                    {
                        foundMine = true;
                    }
                    else if (foundMine && string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        result = msg.GetProperty("text").GetString() ?? "";
                        break;
                    }
                }
            }
            if (result == "")
            {
                Console.WriteLine("messageId from POST: " + messageId);
                Console.WriteLine(historyBody);
                throw new Exception("No Response Message Found");
            }
            
            Bubble.IsOpen = true;
            BubbleText.Text = result;
            Logger.Log(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception");
            Console.WriteLine(ex);
        }
        
        
        
    }
    private void Update(float deltaTime)
    {
        if(laneConnected && LaneEnabled) screenCount -= deltaTime;
        if(screenCount <= 8 && Bubble.IsOpen)
        {
            Bubble.IsOpen = false;
        }
        if(screenCount <= 0)
        {
            WorkWithScreen();
            screenCount = 60f;
        }
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
    private void OnFrame(TimeSpan _)
    {
        if (closed) return;
        TimeSpan now = clock.Elapsed;
        double deltaTime = (now - lastTime).TotalSeconds;
        lastTime = now;

        Update((float)deltaTime);
        ApplyPosition();
        RequestAnimationFrame(OnFrame);
    }
    private void ApplyPosition()
    {
        PixelPoint pos = PixelPoint.FromPoint(new Point(Left, Top), DesktopScaling);
        if (pos != Position) Position = pos;
    }
    private int GetRandomTime()
    {
        return random.Next(5, 6);
    }
    private void Drag(float deltaTime)
    {
        // Place the grabbed point exactly under the cursor instead of nudging by an offset;
        // pointer positions relative to the window can be stale right after it moves, which made it run away
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
        PixelPoint? pixels = GlobalCursor.Position() ?? pointerOnScreen;
        if (pixels is not PixelPoint p) return new Point(Left + grabPoint.X, Top + grabPoint.Y);
        return p.ToPoint(DesktopScaling);
    }
    private void Sprite_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (isDragged) pointerOnScreen = this.PointToScreen(e.GetPosition(this));
    }
    private void Sprite_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!isDragged) return;
        isDragged = false;
        dragPointer?.Capture(null);
        dragPointer = null;
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
        velocity = velocity.WithY(velocity.Y + gravity * deltaTime);

        if(Left <= screen.Left || Left + Width >= screen.Right)
        {
            velocity = velocity.WithX(0);
        }
        else
        {
            velocity = velocity.WithX(velocity.X * Math.Pow(Math.E, -deltaTime * (airFriction / mass)));
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
            velocity = velocity.WithY(0);
        }
        if (selectedAction == Action.Flying && velocity.Length <= throwSpeed)
        {
            selectedAction = Action.Falling;
        }
    }
    void SetSprite(int number)
    {
        // Draw at the image's pixel size; otherwise it is scaled by the file's DPI
        // (a 128px sprite saved at 144 DPI would be drawn only 85px big and float)
        if (Sprite.Source == sprites[number]) return;
        Sprite.Source = sprites[number];
        Sprite.Width = sprites[number].PixelSize.Width;
        Sprite.Height = sprites[number].PixelSize.Height;
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
        // RenderTransformOrigin defaults to the centre in Avalonia
        Sprite.RenderTransform = leftRight == 1 ? faceLeft : faceRight;
    }
    

    private void Sprite_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Sprite).Properties.IsLeftButtonPressed) return;
        actionTime = 0;
        isDragged = true;
        isClimbing = false;
        grabPoint = e.GetPosition(this);
        pointerOnScreen = this.PointToScreen(grabPoint);
        velocity = new Vector();
        dragPointer = e.Pointer;
        dragPointer.Capture(Sprite);
        selectedAction = Action.Carried;

    }
    private void Sprite_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) EndDrag();
    }

    private void LaneToggle_Click(object? sender, RoutedEventArgs e)
    {
        SetLaneEnabled(!LaneEnabled);
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
