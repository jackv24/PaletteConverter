using System;
using System.IO;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;

namespace PaletteConverter
{
	public class Program
	{
		private const int LUT_WIDTH = 256;
		private const int LUT_HEIGHT = 256;

		private static void Main(string[] args)
		{
			string filePath = args[0];

			var colors = new Rgba32[LUT_WIDTH, LUT_HEIGHT];
			int colorsColumnIndex = 0;
			int colorsRowIndex = 0;

			var attr = File.GetAttributes(filePath);
			if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
			{
				Console.WriteLine("Processing directory..");

				string[] files = Directory.GetFiles(filePath, "*.png", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					string newPath = GetFilePathRelative(filePath, file, "Processed");

					if (!ProcessImage(file, newPath, colors, ref colorsColumnIndex, ref colorsRowIndex))
						return;
				}
			}
			else
			{
				Console.WriteLine("Processing individual file..");

				string newPath = filePath.Substring(0, filePath.Length - 4) + "_processed.png";

				if (!ProcessImage(filePath, newPath, colors, ref colorsColumnIndex, ref colorsRowIndex))
					return;
			}

			Image<Rgba32> lut;
			if (colorsRowIndex <= 0)
			{
				lut = new Image<Rgba32>(LUT_WIDTH, 1);
				for (int i = 0; i < LUT_WIDTH; i++)
					lut[i, 0] = i <= colorsColumnIndex ? colors[i, 0] : Rgba32.Black;
			}
			else
			{
				lut = new Image<Rgba32>(LUT_WIDTH, LUT_HEIGHT);
				for (int y = 0; y < LUT_WIDTH; y++)
				{
					for (int x = 0; x < LUT_HEIGHT; x++)
					{
						Rgba32 color;
						if (x > colorsRowIndex || (x == colorsRowIndex && y > colorsColumnIndex))
							color = Rgba32.Black;
						else
							color = colors[y, x];

						lut[y, x] = color;
					}
				}
			}

			string lutPath = "LUT.png";

			lut.Save(lutPath);
			lut.Dispose();
			Console.WriteLine($"Saved {lutPath}");
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

					GetColorIndexes(colors, lutPixel, out int foundColumn, out int foundRow);

					if (foundColumn < 0 || foundRow < 0)
					{
						foundColumn = colorsColumnIndex;
						foundRow = colorsRowIndex;

						colors[foundColumn, foundRow] = lutPixel;

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

			if (!File.Exists(sourcePath))
				throw new FileNotFoundException(sourcePath);

			// trimmedPath becomes the file path with all the subfolders, but without the
			// sourceRootPath that comes in front of it. i.e. it strips the value passed
			// in sourceRootPath from the value passed in sourcePath. The "+ 1" is to include the
			// trailing "\" in the path.
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
	}
}
