namespace Potinho;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--teste")) return Testes.Rodar();

        ApplicationConfiguration.Initialize();
        Pote pote;
        try { pote = Pote.Carregar(Pote.CaminhoPadrao); }
        catch (Exception ex)
        {
            MessageBox.Show($"Não consegui ler seus dados em:\n{Pote.CaminhoPadrao}\n\n{ex.Message}\n\nO arquivo não foi alterado.",
                "Potinho de Pendências", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        Application.Run(new MainForm(pote, Pote.CaminhoPadrao));
        return 0;
    }
}
