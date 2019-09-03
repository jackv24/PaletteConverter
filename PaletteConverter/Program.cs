﻿using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace PaletteConverter
{
	public class Program
	{
		private static void Main(string[] args)
		{
			string filePath = args[0];

			var colors = new Color[256, 256];
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

			Bitmap lut;
			if (colorsRowIndex <= 0)
			{
				lut = new Bitmap(256, 1);
				for (int i = 0; i < 256; i++)
					lut.SetPixel(i, 0, i <= colorsColumnIndex ? colors[i, 0] : Color.Black);
			}
			else
			{
				lut = new Bitmap(256, 256);
				for (int y = 0; y < 256; y++)
				{
					for (int x = 0; x < 256; x++)
					{
						Color color;
						if (y <= colorsColumnIndex && x <= colorsRowIndex)
							color = colors[y, x];
						else
							color = Color.Black;

						lut.SetPixel(y, x, color);
					}
				}
			}

			string lutPath = "LUT.png";

			lut.Save(lutPath, ImageFormat.Png);
			lut.Dispose();
			Console.WriteLine($"Saved {lutPath}");
		}

		private static bool ProcessImage(string filePath, string newPath, Color[,] colors, ref int colorsColumnIndex, ref int colorsRowIndex)
		{
			Console.WriteLine($"Processing file: {filePath}");

			var bmp = new Bitmap(filePath);

			for (int y = 0; y < bmp.Height; y++)
			{
				for (int x = 0; x < bmp.Width; x++)
				{
					var pixel = bmp.GetPixel(x, y);
					var lutPixel = Color.FromArgb(pixel.R, pixel.G, pixel.B);

					int foundColumn;
					int foundRow;
					GetColorIndexes(colors, lutPixel, out foundColumn, out foundRow);

					if (foundColumn < 0 || foundRow < 0)
					{
						foundColumn = colorsColumnIndex;
						foundRow = colorsRowIndex;

						colors[foundColumn, foundRow] = lutPixel;

						colorsColumnIndex++;
						if (colorsColumnIndex >= 256)
						{
							colorsColumnIndex = 0;
							colorsRowIndex++;
						}
					}

					if (colorsColumnIndex >= 256 || colorsRowIndex >= 256)
					{
						Console.WriteLine($"ERROR: More than {256 * 256} colors found!");
						return false;
					}

					//if (foundRow != 0)
						//Console.Write($"Setting pixel at {x}, {y} to R: {foundColumn}, G: {foundRow}");

					var newPixel = Color.FromArgb(255, foundColumn, foundRow, 0);
					bmp.SetPixel(x, y, newPixel);
				}
			}

			bmp.Save(newPath, ImageFormat.Png);
			bmp.Dispose();
			Console.WriteLine($"Saved {newPath}");

			return true;
		}

		private static void GetColorIndexes(Color[,] colors, Color color, out int columnIndex, out int rowIndex)
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