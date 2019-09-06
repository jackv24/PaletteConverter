using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PaletteConverter
{
    public static class ImageProcessor
    {
        // TODO: Automatically determine LUT size
        private const int LUT_WIDTH = 256;
        private const int LUT_HEIGHT = 256;

        public static void ProcessFiles(string filePath)
        {
            // Create array that matches LUT size to store colors while iterating over images
            var colors = new Rgba32[LUT_WIDTH, LUT_HEIGHT];

            // Keep track of the current indexes (since we've already created an array of fixed size)
            int colorsColumnIndex = 0;
            int colorsRowIndex = 0;

            string[] files;
            string rootPath;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string outputDir;

            // If path is a directory, run on all files inside
            var attr = File.GetAttributes(filePath);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                Console.WriteLine("Processing directory..");

                // We only care about PNG files (can't use JPEGs for this, we need absolute correct pixels)
                files = Directory.GetFiles(filePath, "*.png", SearchOption.AllDirectories);

                // A root path was already provided
                rootPath = filePath;

                outputDir = $"{fileName}_processed";
            }
            // If path is a single file, just process the one file
            else
            {
                Console.WriteLine("Processing individual file..");

                files = new[] { filePath };

                // A single file path was provided, so the root path is the folder containing that file
                rootPath = filePath.Substring(0, filePath.Length - Path.GetFileName(filePath).Length);

                outputDir = rootPath;
            }

            if (!ProcessImages(rootPath, outputDir, files, colors, ref colorsColumnIndex, ref colorsRowIndex))
                return;

            string lutPath = Path.Combine(outputDir, $"{fileName}_lut_default.png");
            SaveLUT(lutPath, colors, colorsColumnIndex, colorsRowIndex);
        }

        private static bool ProcessImages(string rootPath, string outputDir, string[] files, Rgba32[,] colors, ref int colorsColumnIndex, ref int colorsRowIndex)
        {
            foreach (var file in files)
            {
                // Preserve folder structure when creating encoded copies
                string newPath = GetFilePathRelative(rootPath, file, outputDir);

                // Keep processing images unless and error is detected
                if (!ProcessImage(file, newPath, colors, ref colorsColumnIndex, ref colorsRowIndex))
                    return false;
            }

            return true;
        }

        private static bool ProcessImage(string filePath, string newPath, Rgba32[,] colors, ref int colorsColumnIndex, ref int colorsRowIndex)
        {
            Console.WriteLine($"Processing file: {filePath}");

            var bmp = Image.Load<Rgba32>(filePath);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixel = bmp[x, y];
                    var lutPixel = new Rgba32(pixel.R, pixel.G, pixel.B);

                    // Find existing color in LUT buffer
                    GetColorIndexes(colors, lutPixel, out int foundColumn, out int foundRow);

                    // Color does not exist in LUT buffer, add it
                    if (foundColumn < 0 || foundRow < 0)
                    {
                        foundColumn = colorsColumnIndex;
                        foundRow = colorsRowIndex;

                        colors[foundColumn, foundRow] = lutPixel;

                        // Increment index, wrapping onto next line if needed (2D LUT will be created)
                        colorsColumnIndex++;
                        if (colorsColumnIndex >= LUT_WIDTH)
                        {
                            colorsColumnIndex = 0;
                            colorsRowIndex++;
                        }
                    }

                    if (colorsRowIndex >= LUT_HEIGHT)
                    {
                        Console.WriteLine($"ERROR: More than {LUT_WIDTH * LUT_HEIGHT} colors found!");
                        return false;
                    }

                    // Encode LUT UV coordinates for pixel into image
                    bmp[x, y] = new Rgba32((float)foundColumn / LUT_WIDTH, (float)foundRow / LUT_HEIGHT, 0, pixel.A);
                }
            }

            bmp.Save(newPath);
            bmp.Dispose();
            Console.WriteLine($"Saved {newPath}");

            return true;
        }

        private static void GetColorIndexes(Rgba32[,] colors, Rgba32 color, out int columnIndex, out int rowIndex)
        {
            // Find color in buffer
            for (int y = 0; y < colors.GetLength(0); y++)
            {
                for (int x = 0; x < colors.GetLength(1); x++)
                {
                    if (colors[y, x] == color)
                    {
                        columnIndex = y;
                        rowIndex = x;
                        return;
                    }
                }
            }

            // Color was not found
            columnIndex = -1;
            rowIndex = -1;
            return;
        }

        private static string GetFilePathRelative(string sourceRootPath, string sourcePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentNullException("sourcePath");

            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentNullException("destinationPath");

            sourceRootPath = Path.GetFullPath(sourceRootPath);
            sourcePath = Path.GetFullPath(sourcePath);
            targetPath = Path.GetFullPath(targetPath);

            // trimmedPath becomes the file path with all the subfolders, but without the
            // sourceRootPath that comes in front of it. i.e. it strips the value passed
            // in sourceRootPath from the value passed in sourcePath.
            string trimmedPath = sourcePath.Substring(sourceRootPath.Length + 1);
            string newPath = Path.Combine(targetPath, trimmedPath);
            string fileName = Path.GetFileName(sourcePath);

            // folderStructure is used for creating the subfolder structure I want to preserve.
            // (It is just removing the file name and extension from the newPath.)
            string folderStructure = newPath.Substring(0, (newPath.Length - fileName.Length));

            // Directory.CreateDirectory will create the entire folder structure for me; no need
            // for looping or recursive calls.
            Directory.CreateDirectory(folderStructure);

            return newPath;
        }

        private static void SaveLUT(string lutPath, Rgba32[,] colors, int lastColumnIndex, int lastRowIndex)
        {
            // Only creat 1D LUT if we can, otherwise create a 2D LUT
            Image<Rgba32> lut;
            if (lastRowIndex <= 0)
            {
                lut = new Image<Rgba32>(LUT_WIDTH, 1);
                for (int i = 0; i < LUT_WIDTH; i++)
                {
                    // Fill LUT with color palette, using black for extra pixels
                    lut[i, 0] = i <= lastColumnIndex ? colors[i, 0] : Rgba32.Black;
                }
            }
            else
            {
                lut = new Image<Rgba32>(LUT_WIDTH, LUT_HEIGHT);
                for (int y = 0; y < LUT_WIDTH; y++)
                {
                    for (int x = 0; x < LUT_HEIGHT; x++)
                    {
                        // Fill LUT with color palette, using black for extra pixels
                        Rgba32 color;
                        if (x > lastRowIndex || (x == lastRowIndex && y > lastColumnIndex))
                            color = Rgba32.Black;
                        else
                            color = colors[y, x];

                        lut[y, x] = color;
                    }
                }
            }

            lut.Save(lutPath);
            lut.Dispose();
            Console.WriteLine($"Saved {lutPath}");
        }
    }
}
