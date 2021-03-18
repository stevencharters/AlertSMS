using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;

namespace AlertSMS
{
    class Logger
    {
        public void WriteLog(string input)
        {
            string filePath = @"C:\EITEPR_LOGS"; //to be held in a webconfig

            DateTime dN = DateTime.Now;

            //current file to write to
            filePath += "\\EitEprLog" + dN.ToString("dd-MM-yyyy", DateTimeFormatInfo.InvariantInfo) + ".txt";
            string logTime = dN.ToString("HH:mm:ss", DateTimeFormatInfo.InvariantInfo) + "  ";

            StreamWriter sw;

            if (File.Exists(filePath)) //file exists to append to it
                sw = File.AppendText(filePath);

            else //file does not exist to create a new one and write data to it
                sw = File.CreateText(filePath);

            sw.Write(logTime);
            sw.WriteLine(input);
            sw.Close();

        }
    }
}
