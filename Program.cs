using System.CommandLine;
using MsixTool.Commands;
using MsixTool.Utils;

namespace MsixTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("MSIX Sparse Package Tool v1.0");
            Console.WriteLine("==============================");
            Console.WriteLine();

            // 检查是否是 --help 或 --version 命令，跳过依赖检查
            bool skipDependencyCheck = args.Length > 0 && 
                (args.Contains("--help") || args.Contains("-h") || args.Contains("-?") || 
                 args.Contains("--version") || args.Contains("deps") || args.Contains("dependency"));

            // 运行依赖检查
            if (!skipDependencyCheck)
            {
                bool depsOk = await DependencyChecker.CheckAllDependenciesAsync(autoInstall: false);
                if (!depsOk)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("依赖检查失败，程序可能无法正常运行。");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("输入 'msixtool deps' 查看详细依赖信息");
                    Console.WriteLine();
                    Console.Write("是否继续运行? (Y/N): ");
                    var key = Console.ReadKey();
                    Console.WriteLine();
                    if (key.Key != ConsoleKey.Y)
                    {
                        return 1;
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ 依赖检查通过");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }

            var rootCommand = new RootCommand("MSIX稀疏包命令行工具 - 支持打包、签名、安装、卸载和证书管理");

            // 创建命令
            var packCommand = CommandBuilder.CreatePackCommand();
            var signCommand = CommandBuilder.CreateSignCommand();
            var installCommand = CommandBuilder.CreateInstallCommand();
            var uninstallCommand = CommandBuilder.CreateUninstallCommand();
            var makecertCommand = CommandBuilder.CreateMakeCertCommand();
            var findCommand = CommandBuilder.CreateFindCommand();
            var packAndInstallCommand = CommandBuilder.CreatePackAndInstallCommand();
            var packAndSignCommand = CommandBuilder.CreatePackAndSignCommand();
            var findCertCommand = CommandBuilder.CreateFindCertCommand();
            var delCertCommand = CommandBuilder.CreateDelCertCommand();
            var depsCommand = CommandBuilder.CreateDepsCommand();

            rootCommand.AddCommand(packCommand);
            rootCommand.AddCommand(signCommand);
            rootCommand.AddCommand(installCommand);
            rootCommand.AddCommand(uninstallCommand);
            rootCommand.AddCommand(makecertCommand);
            rootCommand.AddCommand(findCommand);
            rootCommand.AddCommand(packAndInstallCommand);
            rootCommand.AddCommand(packAndSignCommand);
            rootCommand.AddCommand(findCertCommand);
            rootCommand.AddCommand(delCertCommand);
            rootCommand.AddCommand(depsCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}