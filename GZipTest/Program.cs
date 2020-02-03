using GzipTest.Exceptions;
using System;
using System.IO;

namespace GzipTest
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 3 || (args[0] != "compress" && args[0] != "decompress"))
            {
                Console.WriteLine("Wrong arguments. The arguments must match the following pattern: \n" +
                    "compress/decompress [source file name] [result file name]");
                return 1;
            }

            string action = args[0];

            try
            {
                var sourceFileInfo = new FileInfo(args[1]);
                var resultFileInfo = new FileInfo(args[2]);

                string resultFileDrive = sourceFileInfo.FullName.Substring(0, 3);
                var driveInfo = new DriveInfo(resultFileDrive);
                long freeDriveSpace = driveInfo.AvailableFreeSpace;

                if (sourceFileInfo.FullName == resultFileInfo.FullName)
                {
                    throw new FilesMatchException();
                }

                using (var sourceStream = new FileStream(sourceFileInfo.FullName, FileMode.Open))
                using (var resultStream = new FileStream(resultFileInfo.FullName + ".gz", FileMode.Create))
                {
                    if (sourceStream.Length == 0)
                    {
                        throw new EmptyFileException();
                    }

                    var progressBar = new ProgressBar();
                    var compressor = new Compressor(sourceStream, resultStream, progressBar);

                    if (action == "compress")
                    {
                        if(freeDriveSpace < sourceStream.Length)
                        {
                            throw new NoFreeDriveSpaceException();
                        }

                        Console.WriteLine("Start compression...");
                        compressor.StartCompression();

                        Console.WriteLine("Compression is complete!");
                    }
                    else if (action == "decompress")
                    {
                        if (sourceFileInfo.Extension != ".gz")
                        {
                            throw new FileFormatException();
                        }

                        if (freeDriveSpace < sourceStream.Length * 2)
                        {
                            throw new NoFreeDriveSpaceException();
                        }

                        Console.WriteLine("Start decompression...");
                        compressor.StartDecompression();

                        Console.WriteLine("Decompression is complete!");
                    }
                }
            }
            catch(Exception ex)
            {
                HandleException(ex);

                return 1;
            }

            return 0;
        }

        private static void HandleException(Exception thrownEx)
        {
            Console.Write("An error occured: ");

            switch (thrownEx)
            {
                case FileNotFoundException notFoundEx:
                    Console.WriteLine($"File \"{notFoundEx.FileName}\" does not exist");
                    break;

                case FileCorruptException corruptEx:
                    Console.WriteLine($"The input file is corrupted");
                    break;

                case NoFreeDriveSpaceException driveSpaceEx:
                    Console.WriteLine("No free drive space for file recording");
                    break;

                case EmptyFileException emptyFileEx:
                    Console.WriteLine("Unable to process empty file");
                    break;

                case FilesMatchException filesMatchEx:
                    Console.WriteLine("Source and result files mustn't mutch");
                    break;

                case FileFormatException formatEx:
                    Console.WriteLine("The input file has the invalid format");
                    break;

                case Exception ex:
                    Console.WriteLine($"{ex.Message}");
                    break;
            }
        }
    }
}
