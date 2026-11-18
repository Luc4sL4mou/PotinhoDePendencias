using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Potinho;

// O pote desenhado à mão (GDI+). Tudo é desenhado num espaço fixo de 320x460 e escalado para o tamanho do controle.
public class PoteView : Control
{
    const float L = 320, A = 460;          // espaço de desenho
    const float PW = 280, PH = 64, B = 3;   // papel aberto (design) e densidade do bitmap
    const float SW = 44, SH = 40;           // papelzinho dobrado dentro do pote
    static readonly PointF Mao = new(160, 74), Boca = new(160, 172);
    static readonly Color[] Cores = [Color.FromArgb(252, 252, 248), Color.FromArgb(255, 250, 236), Color.FromArgb(246, 248, 252)];
    static readonly Color Tinta = Color.FromArgb(28, 52, 140);

    readonly Pote pote;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    readonly Stopwatch relogio = new();
    readonly Queue<(float dur, Action<float> passo)> fila = new();
    readonly HashSet<Tarefa> escondidas = [];
    float tempo;

    Tarefa? voando;
    Bitmap? frente, verso;
    float dobra, k, rot, tampa;  // dobra 0 = aberto, 2 = dobrado; k 1 = dobrado grande, 0 = tamanho de papelzinho
    PointF pos;
    bool dentro;

    public Tarefa? NaMao { get; private set; }
    public bool Ocupado => fila.Count > 0;

    public PoteView(Pote pote)
    {
        this.pote = pote;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(250, 244, 232);
        AccessibleName = "Pote de pendências. Clique num papelzinho para tirá-lo.";
        Cursor = Cursors.Hand;
        timer.Tick += (_, _) => Avancar();
    }

    // ---------- animações ----------

    public void Jogar(Tarefa t)
    {
        escondidas.Add(t);
        Etapa(0, _ => { voando = t; NovoPapel(t); pos = Mao; dobra = 0; k = 1; rot = 0; dentro = false; if (NaMao == t) NaMao = null; });
        Etapa(0.9f, p => dobra = 2 * Suave(p));
        Etapa(0.25f, p => tampa = Suave(p));
        Etapa(0.3f, p => { pos = Lerp(Mao, Boca, Suave(p)); k = 1 - p; });
        var (alvo, rotAlvo) = (PointF.Empty, 0f);
        Etapa(0, _ => (alvo, rotAlvo) = Vaga(t));
        Etapa(0.45f, p => { pos = Lerp(Boca, alvo, p * p); rot = rotAlvo * p; dentro = pos.Y > 205; });
        Etapa(0.25f, p => tampa = 1 - Suave(p));
        Etapa(0, _ => { escondidas.Remove(t); voando = null; });
    }

    public void Tirar(Tarefa t)
    {
        escondidas.Add(t);
        var (origem, rotOrigem) = (PointF.Empty, 0f);
        Etapa(0, _ => { (origem, rotOrigem) = Vaga(t); voando = t; NovoPapel(t); pos = origem; rot = rotOrigem; dobra = 2; k = 0; dentro = true; });
        Etapa(0.25f, p => tampa = Suave(p));
        Etapa(0.45f, p => { pos = Lerp(origem, Boca, Suave(p)); rot = rotOrigem * (1 - p); dentro = pos.Y > 205; });
        Etapa(0.3f, p => { pos = Lerp(Boca, Mao, Suave(p)); k = p; });
        Etapa(0.25f, p => tampa = 1 - Suave(p));
        Etapa(0.9f, p => dobra = 2 - 2 * Suave(p));
        Etapa(0, _ => { voando = null; NaMao = t; escondidas.Remove(t); });
    }

    // Devolve o que estiver na mão (se for outro) e tira t.
    public void Pegar(Tarefa t)
    {
        if (t == NaMao) return;
        if (NaMao != null) Jogar(NaMao);
        Tirar(t);
    }

    // Some com o papel da mão (concluída ou removida).
    public void Soltar() { NaMao = null; Invalidate(); }

    void Etapa(float dur, Action<float> passo)
    {
        fila.Enqueue((dur, passo));
        if (!timer.Enabled) { tempo = 0; relogio.Restart(); timer.Start(); Avancar(); }
    }

    void Avancar()
    {
        tempo += (float)relogio.Elapsed.TotalSeconds;
        relogio.Restart();
        while (fila.TryPeek(out var e))
        {
            if (tempo < e.dur) { e.passo(tempo / e.dur); break; }
            e.passo(1);
            tempo -= e.dur;
            fila.Dequeue();
        }
        if (fila.Count == 0) timer.Stop();
        Invalidate();
    }

    static float Suave(float p) => p * p * (3 - 2 * p);
    static PointF Lerp(PointF a, PointF b, float p) => new(a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p);
    static float Lerp(float a, float b, float p) => a + (b - a) * p;
    static int Semente(Tarefa t) => (int)(t.Criada.Ticks ^ (t.Criada.Ticks >> 32));

    // Lugar de cada papelzinho na pilha: 4 por fileira, de baixo para cima, com bagunça fixa por tarefa.
    public (PointF centro, float rot) Vaga(Tarefa t)
    {
        int i = Math.Max(0, pote.Pendentes.IndexOf(t));
        var r = new Random(Semente(t));
        int linha = Math.Min(i / 4, 9), col = i % 4;
        float x = 160 + (col - 1.5f) * 46 + (linha % 2 == 1 ? 10 : -6) + r.Next(-6, 7);
        float y = 410 - linha * 20 + r.Next(-3, 4);
        return (new PointF(x, y), r.Next(-35, 36));
    }

    // ---------- desenho ----------

    Matrix Transformacao()
    {
        float s = Math.Min(Width / L, Height / A);
        var m = new Matrix();
        m.Translate((Width - L * s) / 2, (Height - A * s) / 2);
        m.Scale(s, s);
        return m;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (var m = Transformacao()) g.Transform = m;

        using var corpo = Arredondado(new RectangleF(50, 186, 220, 254), 42, 28);
        using var gargalo = Arredondado(new RectangleF(96, 172, 128, 22), 4, 4);
        using var vidro = new Region(corpo);
        vidro.Union(gargalo);

        // sombra na mesa
        using (var b = new SolidBrush(Color.FromArgb(35, 60, 40, 20))) g.FillEllipse(b, 36, 426, 248, 24);

        // vidro de trás: mais escuro nas bordas para parecer cilíndrico
        using (var b = new LinearGradientBrush(new RectangleF(50, 0, 220, 1), Color.White, Color.White, 0f))
        {
            b.InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(80, 150, 180, 205), Color.FromArgb(22, 210, 230, 245), Color.FromArgb(22, 210, 230, 245), Color.FromArgb(90, 150, 180, 205)],
                Positions = [0, 0.22f, 0.75f, 1],
            };
            g.FillRegion(b, vidro);
        }

        // papelzinhos dentro (recortados pelo vidro)
        var antes = g.Clip;
        g.SetClip(vidro, CombineMode.Intersect);
        foreach (var t in pote.Pendentes)
            if (t != NaMao && !escondidas.Contains(t))
            {
                var (c, r) = Vaga(t);
                Papelzinho(g, c, r, SW, SH, Semente(t));
            }
        if (voando != null && dentro) PapelVoando(g);
        g.Clip = antes;

        if (pote.Pendentes.Count == 0 && voando == null)
            using (var f = new Font("Segoe Script", 13, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Color.FromArgb(120, 90, 110, 130)))
                g.DrawString("vazio...", f, b, new RectangleF(50, 300, 220, 40), new StringFormat { Alignment = StringAlignment.Center });

        VidroDaFrente(g, corpo, gargalo);
        Tampa(g);

        if (NaMao != null && voando == null) DesenharDobra(g, 0, Mao);
        if (voando != null && !dentro) PapelVoando(g);
    }

    static void VidroDaFrente(Graphics g, GraphicsPath corpo, GraphicsPath gargalo)
    {
        using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) g.FillPath(b, corpo);
        // fundo grosso
        using (var b = new SolidBrush(Color.FromArgb(55, 120, 150, 180))) g.FillEllipse(b, 64, 414, 192, 22);
        using (var p = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f)) g.DrawEllipse(p, 64, 414, 192, 22);
        // espessura do vidro
        using (var interno = Arredondado(new RectangleF(57, 196, 206, 238), 36, 23))
        using (var p = new Pen(Color.FromArgb(110, 255, 255, 255), 2.2f)) g.DrawPath(p, interno);
        // brilhos
        using (var brilho = Arredondado(new RectangleF(66, 218, 15, 170), 7, 7))
        using (var b = new LinearGradientBrush(new RectangleF(66, 218, 15, 170), Color.FromArgb(190, 255, 255, 255), Color.FromArgb(30, 255, 255, 255), 90f))
            g.FillPath(b, brilho);
        using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, 69, 397, 9, 9);
        using (var brilho = Arredondado(new RectangleF(247, 236, 7, 120), 3.5f, 3.5f))
        using (var b = new SolidBrush(Color.FromArgb(95, 255, 255, 255))) g.FillPath(b, brilho);
        using (var p = new Pen(Color.FromArgb(150, 255, 255, 255), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(p, 62, 196, 80, 70, 195, 50);
        // contornos
        using var contorno = new Pen(Color.FromArgb(170, 110, 140, 168), 2.2f);
        g.DrawPath(contorno, corpo);
        g.DrawPath(contorno, gargalo);
        using (var p = new Pen(Color.FromArgb(130, 110, 140, 168), 1.5f)) g.DrawEllipse(p, 96, 167, 128, 10); // boca
    }

    void Tampa(Graphics g)
    {
        var estado = g.Save();
        // abre girando pela ponta direita, como uma tampa levantada de lado
        g.TranslateTransform(232, 180);
        g.RotateTransform(82 * tampa);
        g.TranslateTransform(-232 + 6 * tampa, -180 - 6 * tampa);

        var ouro = new ColorBlend
        {
            Colors = [Color.FromArgb(122, 88, 22), Color.FromArgb(214, 176, 70), Color.FromArgb(250, 236, 168), Color.FromArgb(205, 160, 52), Color.FromArgb(150, 108, 28), Color.FromArgb(110, 78, 18)],
            Positions = [0, 0.25f, 0.42f, 0.62f, 0.85f, 1],
        };
        using (var aba = Arredondado(new RectangleF(92, 168, 136, 12), 3, 5))
        using (var b = new LinearGradientBrush(new RectangleF(92, 0, 136, 1), Color.White, Color.White, 0f) { InterpolationColors = ouro })
        {
            g.FillPath(b, aba);
            using var esc = new SolidBrush(Color.FromArgb(60, 60, 35, 0));
            g.FillPath(esc, aba);
        }
        using (var topo = Arredondado(new RectangleF(86, 146, 148, 26), 8, 6))
        using (var b = new LinearGradientBrush(new RectangleF(86, 0, 148, 1), Color.White, Color.White, 0f) { InterpolationColors = ouro })
        {
            g.FillPath(b, topo);
            using var p = new Pen(Color.FromArgb(150, 95, 66, 12), 1.2f);
            g.DrawPath(p, topo);
        }
        using (var p = new Pen(Color.FromArgb(170, 255, 248, 210), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(p, 96, 150.5f, 224, 150.5f);
        using (var p = new Pen(Color.FromArgb(70, 80, 50, 0), 1)) g.DrawLine(p, 90, 166, 230, 166);
        g.Restore(estado);
    }

    void PapelVoando(Graphics g)
    {
        if (dobra < 2) { DesenharDobra(g, dobra, pos); return; }
        Papelzinho(g, pos, rot, Lerp(SW, PW / 4, k), Lerp(SH, PH, k), Semente(voando!));
    }

    // Papel dobrado: retângulo com o vinco no meio, um lado mais sombreado.
    static void Papelzinho(Graphics g, PointF c, float rot, float w, float h, int semente)
    {
        var estado = g.Save();
        g.TranslateTransform(c.X, c.Y);
        g.RotateTransform(rot);
        var r = new Random(semente);
        var cor = Cores[r.Next(Cores.Length)];
        PointF[] borda = [new(-w / 2 + r.Next(3), -h / 2 + r.Next(3)), new(w / 2 - r.Next(3), -h / 2), new(w / 2, h / 2 - r.Next(3)), new(-w / 2, h / 2)];
        using (var b = new SolidBrush(Color.FromArgb(40, 40, 30, 10)))
        {
            g.TranslateTransform(1.5f, 2);
            g.FillPolygon(b, borda);
            g.TranslateTransform(-1.5f, -2);
        }
        using (var b = new SolidBrush(cor)) g.FillPolygon(b, borda);
        using (var b = new SolidBrush(Color.FromArgb(28, 60, 50, 30))) g.FillRectangle(b, -w / 2, -h / 2, w / 2, h);
        using (var p = new Pen(Color.FromArgb(60, 120, 140, 170), 0.6f))
            for (float y = -h / 2 + h / 6; y < h / 2; y += h / 6) g.DrawLine(p, -w / 2 + 1, y, w / 2 - 1, y);
        using (var p = new Pen(Color.FromArgb(90, 90, 80, 60), 0.9f)) g.DrawLine(p, 0, -h / 2, 0, h / 2);
        using (var p = new Pen(Color.FromArgb(110, 120, 115, 105), 0.8f)) g.DrawPolygon(p, borda);
        g.Restore(estado);
    }

    // Dobra em duas etapas: 0→1 dobra a metade direita sobre a esquerda, 1→2 dobra de novo. 2→0 desdobra.
    void DesenharDobra(Graphics g, float f, PointF c)
    {
        if (frente == null || verso == null) return;
        using var escuro = new ImageAttributes();
        escuro.SetColorMatrix(new ColorMatrix { Matrix00 = 0.88f, Matrix11 = 0.88f, Matrix22 = 0.86f, Matrix33 = 1, Matrix44 = 1 });

        bool fase1 = f <= 1;
        float largura = fase1 ? PW : PW / 2, meia = largura / 2;
        float cos = MathF.Cos(MathF.PI * (fase1 ? f : f - 1));
        float extensao = meia + Math.Max(0, meia * cos);
        float x0 = c.X - extensao / 2, y0 = c.Y - PH / 2;
        var baseImg = fase1 ? frente : verso;
        var baseAttr = fase1 ? null : escuro;

        Fatia(g, baseImg, 0, meia, x0, y0, meia, baseAttr);
        if (cos >= 0) Fatia(g, baseImg, meia, meia, x0 + meia, y0, meia * cos, baseAttr);
        else Fatia(g, verso, meia, meia, x0 + meia + meia * cos, y0, -meia * cos, fase1 ? escuro : null);
        // sombra no vinco enquanto a aba está em pé
        float vinco = 1 - Math.Abs(cos);
        using var b = new SolidBrush(Color.FromArgb((int)(50 * vinco), 40, 30, 10));
        g.FillRectangle(b, x0 + meia - 2, y0 + 4, 4, PH - 8);
    }

    static void Fatia(Graphics g, Bitmap img, float srcX, float srcW, float x, float y, float w, ImageAttributes? attr)
    {
        if (w < 0.5f) return;
        PointF[] destino = [new(x, y), new(x + w, y), new(x, y + PH)];
        g.DrawImage(img, destino, new RectangleF(srcX * B, 0, srcW * B, PH * B), GraphicsUnit.Pixel, attr);
    }

    // Tira de papel pautado, rasgado e amassado, com a tarefa em letra cursiva azul.
    void NovoPapel(Tarefa t)
    {
        frente?.Dispose(); verso?.Dispose();
        int sem = Semente(t);
        var cor = Cores[new Random(sem).Next(Cores.Length)];
        frente = DesenharPapel(t, sem, cor, true);
        verso = DesenharPapel(t, sem, cor, false);
    }

    static Bitmap DesenharPapel(Tarefa t, int semente, Color cor, bool comTexto)
    {
        var bmp = new Bitmap((int)(PW * B), (int)(PH * B), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.ScaleTransform(B, B);
        var r = new Random(semente);

        // contorno rasgado: serrilhado miúdo em cima e embaixo, irregular nas pontas
        var pts = new List<PointF>();
        for (float x = 3; x < PW - 3; x += 2.5f + (float)r.NextDouble() * 3) pts.Add(new(x, 2 + (float)r.NextDouble() * 4 + MathF.Sin(x / 37) * 1.5f));
        for (float y = 4; y < PH - 4; y += 5 + r.Next(6)) pts.Add(new(PW - 1 - r.Next(6), y));
        for (float x = PW - 3; x > 3; x -= 2.5f + (float)r.NextDouble() * 3) pts.Add(new(x, PH - 2 - (float)r.NextDouble() * 4 + MathF.Sin(x / 29) * 1.5f));
        for (float y = PH - 4; y > 4; y -= 5 + r.Next(6)) pts.Add(new(1 + r.Next(6), y));
        using var forma = new GraphicsPath();
        forma.AddPolygon(pts.ToArray());

        using (var b = new SolidBrush(cor)) g.FillPath(b, forma);
        g.SetClip(forma);
        // amassado: facetas claras e escuras bem suaves
        for (int i = 0; i < 14; i++)
        {
            float cx = r.Next((int)PW), cy = r.Next((int)PH);
            PointF[] faceta = [new(cx, cy), new(cx + r.Next(-40, 40), cy + r.Next(-30, 30)), new(cx + r.Next(-40, 40), cy + r.Next(-30, 30))];
            using var b = new SolidBrush(i % 2 == 0 ? Color.FromArgb(14, 255, 255, 255) : Color.FromArgb(10, 60, 60, 80));
            g.FillPolygon(b, faceta);
        }
        // pauta
        using (var p = new Pen(Color.FromArgb(comTexto ? 90 : 45, 110, 140, 190), 0.6f))
            for (float y = 9; y < PH; y += 8.5f) g.DrawLine(p, 0, y, PW, y - 2);
        g.ResetClip();
        using (var p = new Pen(Color.FromArgb(120, 135, 135, 135), 0.7f)) g.DrawPath(p, forma);

        if (comTexto)
        {
            g.TranslateTransform(PW / 2, PH / 2);
            g.RotateTransform(-2);
            using var tinta = new SolidBrush(Tinta);
            using var fonte = new Font("Segoe Script", 12.5f, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
            g.DrawString(t.Texto, fonte, tinta, new RectangleF(-PW / 2 + 14, -PH / 2 + 5, PW - 52, PH - 10), fmt);
            using var fxp = new Font("Segoe Script", 9, FontStyle.Bold, GraphicsUnit.Pixel);
            using var ouro = new SolidBrush(Color.FromArgb(170, 120, 20));
            g.DrawString($"{t.Xp} XP", fxp, ouro, new RectangleF(PW / 2 - 44, PH / 2 - 22, 36, 16), new StringFormat { Alignment = StringAlignment.Far });
        }
        return bmp;
    }

    static GraphicsPath Arredondado(RectangleF r, float raioCima, float raioBaixo)
    {
        var p = new GraphicsPath();
        float c = raioCima * 2, b = raioBaixo * 2;
        p.AddArc(r.Left, r.Top, c, c, 180, 90);
        p.AddArc(r.Right - c, r.Top, c, c, 270, 90);
        p.AddArc(r.Right - b, r.Bottom - b, b, b, 0, 90);
        p.AddArc(r.Left, r.Bottom - b, b, b, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---------- clique ----------

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (Ocupado || e.Button != MouseButtons.Left) return;
        using var m = Transformacao();
        m.Invert();
        PointF[] pt = [e.Location];
        m.TransformPoints(pt);
        if (PapelEm(pt[0]) is { } t) Pegar(t);
    }

    // Papelzinho de cima (o último desenhado) que está sob o ponto, em coordenadas de desenho.
    public Tarefa? PapelEm(PointF p)
    {
        for (int i = pote.Pendentes.Count - 1; i >= 0; i--)
        {
            var t = pote.Pendentes[i];
            if (t == NaMao) continue;
            var (c, rot) = Vaga(t);
            float a = -rot * MathF.PI / 180, dx = p.X - c.X, dy = p.Y - c.Y;
            float lx = dx * MathF.Cos(a) - dy * MathF.Sin(a), ly = dx * MathF.Sin(a) + dy * MathF.Cos(a);
            if (Math.Abs(lx) <= SW / 2 && Math.Abs(ly) <= SH / 2) return t;
        }
        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); frente?.Dispose(); verso?.Dispose(); }
        base.Dispose(disposing);
    }
}
