using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;

namespace PowerShellExec
{
    public class PowerShellKey
    {
        public const string Ip = "IP";
        public const string User = "User";
        public const string Password = "Password";
    }

    public class PowerShellExec
    {
        private static readonly string PowerShellCallOperator = "& ";
        private Dictionary<string, string> _parameters;
        private static readonly object locker = new object();

        public PowerShellExec(Dictionary<string, string> parameters)
        {
            _parameters = parameters;
        }

        public int Execute(string command)
        {
            Console.WriteLine($"Executing {command} on {_parameters[PowerShellKey.Ip]}");
            lock (locker)
            {
                Uri remoteComputerUri = new Uri($"http://{_parameters[PowerShellKey.Ip]}:5985/WSMAN");
                SecureString password = new SecureString();
                foreach (var ch in _parameters[PowerShellKey.Password].ToCharArray())
                {
                    password.AppendChar(ch);
                }

                PSCredential pscreds = new PSCredential(_parameters[PowerShellKey.User], password);
                WSManConnectionInfo connectionInfo = new WSManConnectionInfo(remoteComputerUri,
                    @"http://schemas.microsoft.com/powershell/Microsoft.PowerShell", pscreds);
                connectionInfo.OperationTimeout = 2 * 60 * 1000;
                connectionInfo.OpenTimeout = 1 * 60 * 1000;
                connectionInfo.IdleTimeout = 1 * 60 * 1000;
                connectionInfo.CancelTimeout = 1 * 60 * 1000;
                connectionInfo.AuthenticationMechanism = AuthenticationMechanism.Negotiate;
                int exitCode = 0;
                string errorOutputStr = string.Empty;
                string standardOutputStr = string.Empty;
                try
                {
                    Console.WriteLine($"CreateRunspace {_parameters[PowerShellKey.Ip]}");
                    using (Runspace remoteRunspace = RunspaceFactory.CreateRunspace(connectionInfo))
                    {
                        Console.WriteLine($"Open {_parameters[PowerShellKey.Ip]}");
                        remoteRunspace.Open();

                        using (PowerShell remotePwsh = PowerShell.Create())
                        {
                            remotePwsh.Runspace = remoteRunspace;

                            remotePwsh.AddScript(PowerShellCallOperator + command);
                            Console.WriteLine($"Invoke {_parameters[PowerShellKey.Ip]}");
                            Collection<PSObject> output = remotePwsh.Invoke();
                            if (remotePwsh.HadErrors)
                            {
                                foreach (var e in remotePwsh.Streams.Error)
                                {
                                    errorOutputStr += e + Environment.NewLine;
                                }

                                exitCode = 1;
                            }
                            else
                            {
                                foreach (var o in output)
                                {
                                    standardOutputStr += o + Environment.NewLine;
                                }

                                exitCode = 0;
                            }
                        }

                        remoteRunspace.Close();
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    exitCode = -1;
                }

                Console.Error.WriteLine(errorOutputStr);
                Console.WriteLine(standardOutputStr);
                Console.WriteLine($"'{command}' Finished. ExitCode: {exitCode}");
                return exitCode;
            }
        }
    }


    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Type Ip:");
            string ip = Console.ReadLine();
            Console.WriteLine("User:");
            string user = Console.ReadLine();
            Console.WriteLine("Password:");
            string password = Console.ReadLine();
            Console.WriteLine("press any key");
            Console.ReadKey();
            PowerShellExec exec = new PowerShellExec(new Dictionary<string, string>()
            {
                {PowerShellKey.Ip, ip},
                {PowerShellKey.User, user},
                {PowerShellKey.Password, password},
            });

            do
            {
                exec.Execute("ipconfig");
                Thread.Sleep(1000);
            } while (true);
        }
    }
}
