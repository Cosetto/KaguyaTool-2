using System.Text;
using KaguyaArcTool.Commands;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

try
{
    return ProgramMain.Run(args);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
