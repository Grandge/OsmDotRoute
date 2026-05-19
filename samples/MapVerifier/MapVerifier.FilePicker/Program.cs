using System.Text;

namespace MapVerifier.FilePicker;

/// <summary>
/// MapVerifier.Server から起動されてファイル選択ダイアログを表示する GUI プロセス。
/// args: [0]=title, [1]=filter, [2]=initialDir (空文字なら既定), [3]=resultPath (選択結果書込先)
/// exit code: 0=選択, 1=キャンセル, 2=引数不正
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            return 2;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        using var dlg = new OpenFileDialog
        {
            Title = args[0],
            Filter = args[1],
            Multiselect = false,
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(args[2]) && Directory.Exists(args[2]))
        {
            dlg.InitialDirectory = args[2];
        }

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(args[3], dlg.FileName, new UTF8Encoding(true));
            return 0;
        }
        return 1;
    }
}
