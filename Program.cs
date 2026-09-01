using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

enum Direction
{
    North,
    East,
    South,
    West
}

enum TurnIntent
{
    Straight,
    Left,
    Right
}

enum CarState
{
    Moving,
    Waiting,
    Crashed
}

sealed class Car
{
    public int Id { get; init; }
    public Direction CurrentDirection { get; set; }
    public Direction TargetDirection { get; init; }
    public TurnIntent Intent { get; init; }
    public int Lane { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public CarState State { get; set; }
    public double Speed { get; set; } = 7.5;
    public bool HasTurned { get; set; }

    public char Arrow => Intent switch
    {
        TurnIntent.Straight => GetArrow(CurrentDirection),
        _ => HasTurned ? GetArrow(CurrentDirection) : GetArrow(TargetDirection)
    };

    private char GetArrow(Direction dir) => dir switch
    {
        Direction.North => '↑',
        Direction.East => '→',
        Direction.South => '↓',
        _ => '←'
    };
}

sealed class TrafficSimulation
{
    private readonly Random random = new();
    private readonly List<Car> cars = new();
    private readonly int lanes;
    private readonly int width;
    private readonly int height;
    private readonly int centerX;
    private readonly int centerY;
    private readonly int roadHalfWidth;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double spawnTime;
    private double timeScale = 1.0;
    private double carsPerSecond = 1.25;
    private double simulatedSeconds;
    private int nextId = 1;

    public TrafficSimulation(int lanes)
    {
        this.lanes = lanes;
        width = Math.Max(70, Console.WindowWidth - 1);
        height = Math.Max(26, Console.WindowHeight - 1);
        centerX = width / 2;
        centerY = height / 2;
        roadHalfWidth = lanes * 2;
    }

    public void Run()
    {
        Console.CursorVisible = false;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();
        var last = clock.Elapsed.TotalSeconds;
        bool running = true;

        while (running)
        {
            var now = clock.Elapsed.TotalSeconds;
            var delta = Math.Min(0.1, now - last);
            last = now;

            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key is ConsoleKey.Q or ConsoleKey.Escape)
                {
                    running = false;
                    break;
                }
                if (key == ConsoleKey.UpArrow)
                    timeScale += 1.0;
                if (key == ConsoleKey.DownArrow)
                    timeScale = Math.Max(0.1, timeScale - 1.0);
                if (key == ConsoleKey.RightArrow)
                    carsPerSecond += 0.5;
                if (key == ConsoleKey.LeftArrow)
                    carsPerSecond = Math.Max(0.1, carsPerSecond - 0.5);
            }

            if (!running) break;

            Update(delta * timeScale);
            Render();
            Thread.Sleep(50);
        }

        Console.ResetColor();
        Console.CursorVisible = true;
        Console.Clear();
        Console.WriteLine("Traffic simulation ended.");
    }

    private void Update(double delta)
    {
        spawnTime += delta;
        simulatedSeconds += delta * 60;
        
        double currentSpawnInterval = 1.0 / carsPerSecond;
        while (spawnTime >= currentSpawnInterval)
        {
            spawnTime -= currentSpawnInterval;
            SpawnCars();
        }

        foreach (var car in cars)
            UpdateCar(car, delta);

        DetectCollisions();
        cars.RemoveAll(car => IsOutside(car));
    }

    private void SpawnCars()
    {
        var direction = (Direction)random.Next(4);
        var lane = random.Next(lanes);
        var intent = (TurnIntent)random.Next(3);
        
        var targetDirection = direction;
        if (intent == TurnIntent.Left)
        {
            targetDirection = (Direction)(((int)direction + 3) % 4);
        }
        else if (intent == TurnIntent.Right)
        {
            targetDirection = (Direction)(((int)direction + 1) % 4);
        }

        var car = new Car 
        { 
            Id = nextId++, 
            CurrentDirection = direction, 
            TargetDirection = targetDirection,
            Intent = intent, 
            Lane = lane 
        };

        switch (direction)
        {
            case Direction.North:
                car.X = centerX + 1 + lane * 2;
                car.Y = height;
                break;
            case Direction.East:
                car.X = 0;
                car.Y = centerY + 1 + lane * 2;
                break;
            case Direction.South:
                car.X = centerX - 1 - lane * 2;
                car.Y = 0;
                break;
            case Direction.West:
                car.X = width;
                car.Y = centerY - 1 - lane * 2;
                break;
        }
        cars.Add(car);
    }

    private void UpdateCar(Car car, double delta)
    {
        if (car.State == CarState.Crashed)
        {
            return;
        }

        bool obstacleAhead = false;
        foreach (var other in cars)
        {
            if (other == car) continue;
            
            if (other.CurrentDirection == car.CurrentDirection && other.Lane == car.Lane)
            {
                if (car.CurrentDirection == Direction.North && other.Y < car.Y && other.Y > car.Y - 2.5) obstacleAhead = true;
                if (car.CurrentDirection == Direction.South && other.Y > car.Y && other.Y < car.Y + 2.5) obstacleAhead = true;
                if (car.CurrentDirection == Direction.East && other.X > car.X && other.X < car.X + 2.5) obstacleAhead = true;
                if (car.CurrentDirection == Direction.West && other.X < car.X && other.X > car.X - 2.5) obstacleAhead = true;
            }
        }

        if (obstacleAhead)
        {
            car.State = CarState.Waiting;
            return;
        }

        car.State = CarState.Moving;
        var distance = car.Speed * delta;

        if (!car.HasTurned && car.Intent != TurnIntent.Straight)
        {
            bool timeToTurn = false;
            int offset = 1 + car.Lane * 2;
            
            switch (car.CurrentDirection)
            {
                case Direction.North:
                    if (car.Y <= centerY + (car.Intent == TurnIntent.Left ? -offset : offset)) timeToTurn = true;
                    break;
                case Direction.South:
                    if (car.Y >= centerY + (car.Intent == TurnIntent.Left ? offset : -offset)) timeToTurn = true;
                    break;
                case Direction.East:
                    if (car.X >= centerX + (car.Intent == TurnIntent.Left ? offset : -offset)) timeToTurn = true;
                    break;
                case Direction.West:
                    if (car.X <= centerX + (car.Intent == TurnIntent.Left ? -offset : offset)) timeToTurn = true;
                    break;
            }

            if (timeToTurn)
            {
                car.CurrentDirection = car.TargetDirection;
                car.HasTurned = true;
                
                if (car.CurrentDirection is Direction.North or Direction.South)
                {
                    car.X = centerX + (car.CurrentDirection == Direction.North ? offset : -offset);
                }
                else
                {
                    car.Y = centerY + (car.CurrentDirection == Direction.East ? offset : -offset);
                }
            }
        }

        switch (car.CurrentDirection)
        {
            case Direction.North: car.Y -= distance; break;
            case Direction.East: car.X += distance; break;
            case Direction.South: car.Y += distance; break;
            case Direction.West: car.X -= distance; break;
        }
    }

    private void DetectCollisions()
    {
        bool overlapFound = true;
        int iterations = 0;

        while (overlapFound && iterations < 10)
        {
            overlapFound = false;
            iterations++;

            for (var first = 0; first < cars.Count; first++)
            {
                for (var second = first + 1; second < cars.Count; second++)
                {
                    var a = cars[first];
                    var b = cars[second];

                    bool proximity = Math.Abs(a.X - b.X) < 0.8 && Math.Abs(a.Y - b.Y) < 0.8;
                    bool exactOverlap = (int)Math.Round(a.X) == (int)Math.Round(b.X) && (int)Math.Round(a.Y) == (int)Math.Round(b.Y);

                    if (proximity || exactOverlap)
                    {
                        a.State = CarState.Crashed;
                        b.State = CarState.Crashed;
                    }

                    if (exactOverlap)
                    {
                        overlapFound = true;
                        switch (a.CurrentDirection)
                        {
                            case Direction.North: a.Y += 1.0; break;
                            case Direction.East: a.X -= 1.0; break;
                            case Direction.South: a.Y -= 1.0; break;
                            case Direction.West: a.X += 1.0; break;
                        }
                    }
                }
            }
        }
    }

    private bool IsOutside(Car car)
    {
        return car.X < -5 || car.X > width + 5 || car.Y < -5 || car.Y > height + 5;
    }

    private void Render()
    {
        var buffer = new char[height, width];
        var fColors = new ConsoleColor[height, width];
        var bColors = new ConsoleColor[height, width];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[y, x] = ' ';
                fColors[y, x] = ConsoleColor.Black;
                bColors[y, x] = ConsoleColor.Black;
            }
        }

        DrawRoad(buffer, fColors, bColors);
        DrawStatsBox(buffer, fColors, bColors);
        DrawControls(buffer, fColors, bColors);

        foreach (var car in cars)
            DrawCar(buffer, fColors, bColors, car);

        Console.SetCursorPosition(0, 0);
        var sb = new StringBuilder();
        var currentFg = ConsoleColor.White;
        var currentBg = ConsoleColor.Black;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var fg = fColors[y, x];
                var bg = bColors[y, x];
                var c = buffer[y, x];

                if (fg != currentFg || bg != currentBg)
                {
                    if (sb.Length > 0)
                    {
                        Console.ForegroundColor = currentFg;
                        Console.BackgroundColor = currentBg;
                        Console.Write(sb.ToString());
                        sb.Clear();
                    }
                    currentFg = fg;
                    currentBg = bg;
                }
                sb.Append(c);
            }
            if (y < height - 1)
            {
                if (sb.Length > 0)
                {
                    Console.ForegroundColor = currentFg;
                    Console.BackgroundColor = currentBg;
                    Console.Write(sb.ToString());
                    sb.Clear();
                }
                Console.WriteLine();
            }
        }
        
        if (sb.Length > 0)
        {
            Console.ForegroundColor = currentFg;
            Console.BackgroundColor = currentBg;
            Console.Write(sb.ToString());
        }
    }

    private void DrawStatsBox(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors)
    {
        int bw = 22;
        int bh = 5;
        int bx = width - bw;
        int by = 0;

        Put(buffer, fColors, bColors, bx, by, '┌', ConsoleColor.White, ConsoleColor.Black);
        Put(buffer, fColors, bColors, bx + bw - 1, by, '┐', ConsoleColor.White, ConsoleColor.Black);
        Put(buffer, fColors, bColors, bx, by + bh - 1, '└', ConsoleColor.White, ConsoleColor.Black);
        Put(buffer, fColors, bColors, bx + bw - 1, by + bh - 1, '┘', ConsoleColor.White, ConsoleColor.Black);

        for (int i = 1; i < bw - 1; i++)
        {
            Put(buffer, fColors, bColors, bx + i, by, '─', ConsoleColor.White, ConsoleColor.Black);
            Put(buffer, fColors, bColors, bx + i, by + bh - 1, '─', ConsoleColor.White, ConsoleColor.Black);
        }
        for (int i = 1; i < bh - 1; i++)
        {
            Put(buffer, fColors, bColors, bx, by + i, '│', ConsoleColor.White, ConsoleColor.Black);
            Put(buffer, fColors, bColors, bx + bw - 1, by + i, '│', ConsoleColor.White, ConsoleColor.Black);
        }

        var time = TimeSpan.FromSeconds(simulatedSeconds);
        string timeStr = $"TIME   {(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        string speedStr = $"SPEED  {timeScale:F1}x";
        string cpsStr = $"CARS/S {carsPerSecond:F2}";

        DrawString(buffer, fColors, bColors, bx + 2, by + 1, timeStr, ConsoleColor.Cyan, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, bx + 2, by + 2, speedStr, ConsoleColor.Yellow, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, bx + 2, by + 3, cpsStr, ConsoleColor.Green, ConsoleColor.Black);
    }

    private void DrawControls(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors)
    {
        DrawString(buffer, fColors, bColors, 0, 0, $"LANES: {lanes}   CARS: {cars.Count}", ConsoleColor.White, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, 0, 1, "CONTROLS:", ConsoleColor.White, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, 2, 2, "UP/DOWN    : SPEED", ConsoleColor.Gray, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, 2, 3, "LEFT/RIGHT : CARS/SEC", ConsoleColor.Gray, ConsoleColor.Black);
        DrawString(buffer, fColors, bColors, 2, 4, "Q/ESC      : EXIT", ConsoleColor.Gray, ConsoleColor.Black);
    }

    private void DrawString(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors, int x, int y, string str, ConsoleColor fg, ConsoleColor bg)
    {
        for (int i = 0; i < str.Length; i++)
        {
            if (x + i < width)
            {
                Put(buffer, fColors, bColors, x + i, y, str[i], fg, bg);
            }
        }
    }

    private void DrawRoad(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors)
    {
        for (var y = 0; y < height; y++)
        {
            if (y < centerY - roadHalfWidth || y > centerY + roadHalfWidth)
            {
                Put(buffer, fColors, bColors, centerX - roadHalfWidth, y, '│', ConsoleColor.DarkGray, ConsoleColor.Black);
                Put(buffer, fColors, bColors, centerX + roadHalfWidth, y, '│', ConsoleColor.DarkGray, ConsoleColor.Black);
                Put(buffer, fColors, bColors, centerX, y, '│', ConsoleColor.Yellow, ConsoleColor.Black);
                
                for (int l = 1; l < lanes; l++)
                {
                    if (y % 4 < 2)
                    {
                        Put(buffer, fColors, bColors, centerX - 2 * l, y, '│', ConsoleColor.White, ConsoleColor.Black);
                        Put(buffer, fColors, bColors, centerX + 2 * l, y, '│', ConsoleColor.White, ConsoleColor.Black);
                    }
                }
            }
        }

        for (var x = 0; x < width; x++)
        {
            if (x < centerX - roadHalfWidth || x > centerX + roadHalfWidth)
            {
                Put(buffer, fColors, bColors, x, centerY - roadHalfWidth, '─', ConsoleColor.DarkGray, ConsoleColor.Black);
                Put(buffer, fColors, bColors, x, centerY + roadHalfWidth, '─', ConsoleColor.DarkGray, ConsoleColor.Black);
                Put(buffer, fColors, bColors, x, centerY, '─', ConsoleColor.Yellow, ConsoleColor.Black);
                
                for (int l = 1; l < lanes; l++)
                {
                    if (x % 4 < 2)
                    {
                        Put(buffer, fColors, bColors, x, centerY - 2 * l, '─', ConsoleColor.White, ConsoleColor.Black);
                        Put(buffer, fColors, bColors, x, centerY + 2 * l, '─', ConsoleColor.White, ConsoleColor.Black);
                    }
                }
            }
        }

        Put(buffer, fColors, bColors, centerX - roadHalfWidth, centerY - roadHalfWidth, '┌', ConsoleColor.DarkGray, ConsoleColor.Black);
        Put(buffer, fColors, bColors, centerX + roadHalfWidth, centerY - roadHalfWidth, '┐', ConsoleColor.DarkGray, ConsoleColor.Black);
        Put(buffer, fColors, bColors, centerX - roadHalfWidth, centerY + roadHalfWidth, '└', ConsoleColor.DarkGray, ConsoleColor.Black);
        Put(buffer, fColors, bColors, centerX + roadHalfWidth, centerY + roadHalfWidth, '┘', ConsoleColor.DarkGray, ConsoleColor.Black);
    }

    private void DrawCar(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors, Car car)
    {
        var x = (int)Math.Round(car.X);
        var y = (int)Math.Round(car.Y);

        ConsoleColor fgColor = ConsoleColor.Green;
        ConsoleColor bgColor = ConsoleColor.Black;

        if (car.State == CarState.Crashed)
        {
            fgColor = ConsoleColor.Red;
        }
        else if (!car.HasTurned && car.Intent != TurnIntent.Straight)
        {
            fgColor = ConsoleColor.Yellow;
        }

        Put(buffer, fColors, bColors, x, y, car.Arrow, fgColor, bgColor);
    }

    private void Put(char[,] buffer, ConsoleColor[,] fColors, ConsoleColor[,] bColors, int x, int y, char value, ConsoleColor fg, ConsoleColor bg)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            buffer[y, x] = value;
            fColors[y, x] = fg;
            bColors[y, x] = bg;
        }
    }
}

static class Program
{
    public static void Main()
    {
        TryMaximizeWindow();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.CursorVisible = true;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("TRAFFIC INTERSECTION SIMULATOR");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Configure the intersection before starting.");
        Console.Write("Number of lanes per direction (1 to 4): ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out var lanes) || lanes is < 1 || lanes > 4)
        {
            Console.WriteLine("Please enter a whole number from 1 to 4.");
            return;
        }

        new TrafficSimulation(lanes).Run();
    }

    private static void TryMaximizeWindow()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Console.SetWindowSize(Console.LargestWindowWidth, Console.LargestWindowHeight);
            }
            else
            {
                Console.Write("\x1b[9;1t");
            }
        }
        catch (IOException) { }
        catch (ArgumentOutOfRangeException) { }
    }
}