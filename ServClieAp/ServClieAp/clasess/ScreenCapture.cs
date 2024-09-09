using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ServClieAp.clasess
{
    internal class ScreenCapture
    {
        private static int screenshotCounter=0;
       private static string nircmdPath = @"./nircmd.exe";

        public  static string CaptureScreen()
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string screenshotPath = $"screenshot{screenshotCounter}.png";
            string filePathScreen = Path.Combine(exeDirectory, screenshotPath); // Постройте абсолютный путь к вашему файлу
            string screenFullPath = filePathScreen;
            string arguments = $"savescreenshot \"{filePathScreen}\"";
           
            string filePathNir = Path.Combine(exeDirectory, nircmdPath); // Постройте абсолютный путь к вашему файлу
            string nirFullPath = filePathNir;
            try
            {
              

                // Start nircmd with the specified arguments
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = nirFullPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                try
                {
                    using (Process process = Process.Start(processStartInfo))
                    {
                        process.WaitForExit();

                        // Read the output and errors if needed
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        if (process.ExitCode == 0)
                        {
                            Console.WriteLine("Screenshot successfully taken: " + screenshotPath);
                            
                        }
                        else
                        {
                            Console.WriteLine("An error occurred while taking the screenshot:");
                            Console.WriteLine(error);
                            return $"Error: {error}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred while starting nircmd:");
                    Console.WriteLine(ex.Message);
                    return $"Error: {ex.Message}";
                }
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
           
            screenshotCounter++;
            return screenFullPath;
        }
    }
}
