// Main.cs
// Created by Oleg Yaroshevych at 00:52 09/27/2009

using System;
using System.IO;

namespace Converter
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			if (args.Length == 0) {
				Console.WriteLine("Please specify DB file path as parameter.");
				Console.WriteLine("Usage: m2p-converter.exe [PATH]");
				return;
			}
			
			if (!File.Exists(args[0])) {
				Console.WriteLine(string.Format("Error: file {0} not found.", args[0]));
				return;
			}
			
			#if DEBUG
			Console.WriteLine("Starting...");
			#endif
			
			try {
				Database db = new Database(args[0], System.Text.Encoding.GetEncoding (1251));
				var protocols = db.Read();
				
				string outputPath = Path.Combine(Environment.CurrentDirectory, "logs");
				OutputFileHelper.WriteToFiles(outputPath, protocols);
			}
			catch (Exception ex) {
				Console.WriteLine("Error occured: " + ex.Message);
			}
			
			#if DEBUG
			Console.WriteLine("Finished.");
			#endif
		}
	}
}
