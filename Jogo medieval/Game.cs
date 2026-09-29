using System.Numerics;
using Raylib_cs;

/// Efeito visual temporário (golpes).
class Effect
{
    public Rectangle Rect;
    public Color Color;
    public float ExpireAt;
}

/// Guarda o mundo: mapa, player, inimigos, tempo.
class Game
{
    public const int ScreenW = 1280;
    public const int ScreenH = 720;
    const string LevelPath = "Assets/level1.json";

    public float Now;
    public Player Player = null!;
    public LevelData Level = null!;
    public List<Enemy> Enemies = new();
    public List<Rectangle> Solids = new();
    public List<Effect> Effects = new();

    float deadFor;
    bool showCollision;   // F1 liga/desliga

    public Game() => Reset();

    public void Reset()
    {
        Now = 0;
        deadFor = 0;
        Enemies.Clear();
        Solids.Clear();
        Effects.Clear();

        // ---- MAPA vem do Tiled ----
        Level = TiledLoader.Load(LevelPath);
        Solids.AddRange(Level.Solids);

        // ---- SPAWNS: o ponto (x,y) do Tiled é onde os PÉS da entidade ficam ----
        Player = new Player(100, 100); // provisório, ajustado abaixo
        foreach (var s in Level.Spawns)
        {
            switch (s.Kind)
            {
                case "PlayerSpawn":
                    Player = new Player(s.X - 14, s.Y - 56);              // player 28x56
                    break;
                case "Enemy":
                    Enemies.Add(new Enemy(this, s.X - 16, s.Y - 56));     // inimigo 32x56
                    break;
                case "Boss":
                    Enemies.Add(new Enemy(this, s.X - 23, s.Y - 80, boss: true)); // boss 46x80
                    break;
            }
        }
    }

    public void AddEffect(Rectangle r, Color c, float duration)
        => Effects.Add(new Effect { Rect = r, Color = c, ExpireAt = Now + duration });

    /// Retorna true se a fase foi reiniciada neste frame.
    public bool Update(float dt)
    {
        Now += dt;

        if (Raylib.IsKeyPressed(KeyboardKey.F1)) showCollision = !showCollision;

        Player.Update(this, dt);
        foreach (var e in Enemies) e.Update(this, dt);

        // caiu do mapa = morreu
        float killY = Level.PixelHeight + 300;
        if (Player.Y > killY && !Player.Dead) { Player.HP = 0; Player.Dead = true; }
        foreach (var e in Enemies)
            if (e.Y > killY && !e.Dead) { e.HP = 0; e.Dead = true; }

        Enemies.RemoveAll(e => e.Dead && e.DeadTimer > 1f);
        Effects.RemoveAll(e => Now > e.ExpireAt);

        if (Player.Dead)
        {
            deadFor += dt;
            if (deadFor > 3f) { Reset(); return true; }
        }
        return false;
    }

    public void DrawWorld(Camera2D cam)
    {
        // área visível do mundo (pra só desenhar os tiles que aparecem)
        var view = new Rectangle(
            cam.Target.X - cam.Offset.X / cam.Zoom,
            cam.Target.Y - cam.Offset.Y / cam.Zoom,
            ScreenW / cam.Zoom, ScreenH / cam.Zoom);

        if (Level.HasArt)
            Level.Draw(view, foreground: false);
        else
            foreach (var s in Solids)
                Raylib.DrawRectangleRec(s, new Color(64, 56, 51, 255));

        foreach (var e in Enemies) e.Draw(Now);
        Player.Draw(Now);

        foreach (var fx in Effects)
            Raylib.DrawRectangleRec(fx.Rect, fx.Color);

        if (Level.HasArt)
            Level.Draw(view, foreground: true);   // camadas "fg..." por cima de tudo

        if (showCollision)
            foreach (var s in Solids)
                Raylib.DrawRectangleLinesEx(s, 1f, new Color(0, 255, 0, 200));
    }

    public void DrawHud()
    {
        var p = Player;

        // vida
        Raylib.DrawRectangle(18, 18, 304, 24, new Color(0, 0, 0, 160));
        Raylib.DrawRectangle(20, 20, (int)(300 * (p.HP / p.MaxHP)), 20, new Color(200, 25, 25, 255));
        // stamina
        Raylib.DrawRectangle(18, 46, 224, 16, new Color(0, 0, 0, 160));
        Raylib.DrawRectangle(20, 48, (int)(220 * (p.Stamina / Player.MaxStamina)), 12, new Color(50, 200, 80, 255));

        Raylib.DrawText($"Frascos: {p.Flasks}    Almas: {p.Souls}", 20, 70, 20, Color.White);
        Raylib.DrawText("A/D mover | Espaco pular | Shift rolar | J atacar | Q curar | F1 colisao",
            20, ScreenH - 30, 18, Color.LightGray);

        if (p.Dead)
        {
            const string txt = "VOCE MORREU";
            int w = Raylib.MeasureText(txt, 80);
            Raylib.DrawText(txt, (ScreenW - w) / 2, ScreenH / 2 - 40, 80, new Color(180, 20, 20, 255));
        }
    }
}
