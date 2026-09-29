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

    public float Now;
    public Player Player = null!;
    public List<Enemy> Enemies = new();
    public List<Rectangle> Solids = new();
    public List<Effect> Effects = new();

    float deadFor;

    public Game() => Reset();

    public void Reset()
    {
        Now = 0;
        deadFor = 0;
        Enemies.Clear();
        Solids.Clear();
        Effects.Clear();

        // ---- MAPA (x, y, largura, altura) ----
        Solids.Add(new Rectangle(-800, 500, 3000, 300));   // chão
        Solids.Add(new Rectangle(-840, -300, 40, 800));    // parede esquerda
        Solids.Add(new Rectangle(2200, -300, 40, 800));    // parede direita
        Solids.Add(new Rectangle(300, 390, 200, 20));      // plataformas
        Solids.Add(new Rectangle(560, 300, 160, 20));
        Solids.Add(new Rectangle(1100, 390, 240, 20));

        // ---- PLAYER ----
        Player = new Player(-600, 440);

        // ---- INIMIGOS ----
        Enemies.Add(new Enemy(this, 100, 440));
        Enemies.Add(new Enemy(this, 800, 440));
        Enemies.Add(new Enemy(this, 1200, 330));
        Enemies.Add(new Enemy(this, 1700, 420, boss: true)); // mini-boss
    }

    public void AddEffect(Rectangle r, Color c, float duration)
        => Effects.Add(new Effect { Rect = r, Color = c, ExpireAt = Now + duration });

    /// Retorna true se a fase foi reiniciada neste frame.
    public bool Update(float dt)
    {
        Now += dt;

        Player.Update(this, dt);
        foreach (var e in Enemies) e.Update(this, dt);

        Enemies.RemoveAll(e => e.Dead && e.DeadTimer > 1f);
        Effects.RemoveAll(e => Now > e.ExpireAt);

        if (Player.Dead)
        {
            deadFor += dt;
            if (deadFor > 3f) { Reset(); return true; }
        }
        return false;
    }

    public void DrawWorld()
    {
        foreach (var s in Solids)
            Raylib.DrawRectangleRec(s, new Color(64, 56, 51, 255));

        foreach (var e in Enemies) e.Draw(Now);
        Player.Draw(Now);

        foreach (var fx in Effects)
            Raylib.DrawRectangleRec(fx.Rect, fx.Color);
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
        Raylib.DrawText("A/D mover | Espaco pular | Shift rolar | J atacar | Q curar",
            20, ScreenH - 30, 18, Color.LightGray);

        if (p.Dead)
        {
            const string txt = "VOCE MORREU";
            int w = Raylib.MeasureText(txt, 80);
            Raylib.DrawText(txt, (ScreenW - w) / 2, ScreenH / 2 - 40, 80, new Color(180, 20, 20, 255));
        }
    }
}
