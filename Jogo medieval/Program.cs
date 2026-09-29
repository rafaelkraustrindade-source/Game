using System.Numerics;
using Raylib_cs;

Raylib.InitWindow(Game.ScreenW, Game.ScreenH, "Soulslike Base");
Raylib.SetTargetFPS(60);

var game = new Game();          // precisa vir DEPOIS do InitWindow (carrega texturas)
Vector2 camPos = game.Player.Center;
var cam = new Camera2D
{
    Offset = new Vector2(Game.ScreenW / 2f, Game.ScreenH / 2f),
    Target = camPos,
    Rotation = 0f,
    Zoom = 1f
};

while (!Raylib.WindowShouldClose())
{
    float dt = MathF.Min(Raylib.GetFrameTime(), 1f / 30f);
    bool restarted = game.Update(dt);

    Vector2 goal = game.Player.Center + new Vector2(0, -60);
    camPos = restarted ? goal : Vector2.Lerp(camPos, goal, 8f * dt);

    // arredonda pra pixel inteiro: evita "linhas" entre os tiles
    cam.Target = new Vector2(MathF.Round(camPos.X), MathF.Round(camPos.Y));

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(15, 15, 26, 255));

    Raylib.BeginMode2D(cam);
    game.DrawWorld(cam);
    Raylib.EndMode2D();

    game.DrawHud();
    Raylib.EndDrawing();
}

Raylib.CloseWindow();
