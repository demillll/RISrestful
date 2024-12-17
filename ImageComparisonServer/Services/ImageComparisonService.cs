using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using Microsoft.Extensions.Logging;

namespace ImageComparisonServer.Services
{
	public interface IImageComparisonService
	{
		double CompareHistograms(float[] hist1, float[] hist2);
		float[] CalculateNormalizedHistogramSequential(Image<Rgba32> image);
		float[] CalculateNormalizedHistogramParallel(Image<Rgba32> image);
		(double similarity, long linearTime, long parallelTime) BenchmarkComparison(Image<Rgba32> img1, Image<Rgba32> img2);
		List<(int x, int y, double similarity)> CompareImageBlocks(Image<Rgba32> img1, Image<Rgba32> img2, int blockSize = 50);
	}

	public class ImageComparisonService : IImageComparisonService
	{
		private readonly ILogger<ImageComparisonService> _logger;

		public ImageComparisonService(ILogger<ImageComparisonService> logger)
		{
			_logger = logger;
		}

		public float[] CalculateNormalizedHistogramSequential(Image<Rgba32> image)
		{
			_logger.LogInformation("Начало расчета гистограммы для изображения (последовательный метод).");

			int[] histogram = new int[256];
			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (int x = 0; x < row.Length; x++)
					{
						var pixel = row[x];
						int grayValue = (pixel.R + pixel.G + pixel.B) / 3;
						Interlocked.Increment(ref histogram[grayValue]);
					}
				}
			});

			float[] normalizedHistogram = new float[256];
			int totalPixels = image.Width * image.Height;
			for (int i = 0; i < histogram.Length; i++)
			{
				normalizedHistogram[i] = histogram[i] / (float)totalPixels;
			}

			_logger.LogInformation("Гистограмма для изображения рассчитана (последовательный метод).");
			return normalizedHistogram;
		}

		public float[] CalculateNormalizedHistogramParallel(Image<Rgba32> image)
		{
			_logger.LogInformation("Начало расчета гистограммы для изображения (параллельный метод).");

			int[] globalHistogram = new int[256];
			int height = image.Height;
			int width = image.Width;
			int numThreads = Environment.ProcessorCount; // Количество потоков
			int blockSize = height / numThreads; // Строк на поток

			Parallel.For(0, numThreads, threadIndex =>
			{
				int startY = threadIndex * blockSize;
				int endY = (threadIndex == numThreads - 1) ? height : startY + blockSize;

				int[] localHistogram = new int[256];

				image.ProcessPixelRows(accessor =>
				{
					for (int y = startY; y < endY; y++)
					{
						var row = accessor.GetRowSpan(y);
						for (int x = 0; x < row.Length; x++)
						{
							var pixel = row[x];
							int grayValue = (pixel.R + pixel.G + pixel.B) / 3; // Преобразование в оттенок серого
							localHistogram[grayValue]++;
						}
					}
				});

				// Слияние локальной гистограммы с глобальной
				for (int i = 0; i < localHistogram.Length; i++)
				{
					Interlocked.Add(ref globalHistogram[i], localHistogram[i]);
				}
			});

			// Нормализация гистограммы
			float[] normalizedHistogram = new float[256];
			int totalPixels = width * height;

			for (int i = 0; i < globalHistogram.Length; i++)
			{
				normalizedHistogram[i] = globalHistogram[i] / (float)totalPixels;
			}

			_logger.LogInformation("Гистограмма для изображения рассчитана (параллельный метод).");
			return normalizedHistogram;
		}

		public double CompareHistograms(float[] hist1, float[] hist2)
		{
			if (hist1 == null || hist2 == null || hist1.Length != hist2.Length)
			{
				_logger.LogError("Гистограммы должны быть не null и одинаковой длины.");
				throw new ArgumentException("Гистограммы должны быть не null и одинаковой длины.");
			}

			double dotProduct = 0;
			double magnitude1 = 0;
			double magnitude2 = 0;

			for (int i = 0; i < hist1.Length; i++)
			{
				dotProduct += hist1[i] * hist2[i];
				magnitude1 += hist1[i] * hist1[i];
				magnitude2 += hist2[i] * hist2[i];
			}

			if (magnitude1 == 0 || magnitude2 == 0)
			{
				_logger.LogWarning("Одна из гистограмм пуста (нулевая длина). Возвращаемая схожесть: 0.");
				return 0;
			}

			double similarity = dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
			_logger.LogInformation("Схожесть гистограмм: {Similarity}%", similarity * 100);
			return similarity * 100;
		}

		public List<(int x, int y, double similarity)> CompareImageBlocks(Image<Rgba32> img1, Image<Rgba32> img2, int blockSize = 50)
		{
			_logger.LogInformation("Начало сравнения блоков изображения.");
			var results = new List<(int x, int y, double similarity)>();

			for (int y = 0; y < img1.Height; y += blockSize)
			{
				for (int x = 0; x < img1.Width; x += blockSize)
				{
					int blockWidth = Math.Min(blockSize, img1.Width - x);
					int blockHeight = Math.Min(blockSize, img1.Height - y);

					int[] hist1 = new int[256];
					int[] hist2 = new int[256];

					for (int i = 0; i < blockHeight; i++)
					{
						img1.ProcessPixelRows(accessor =>
						{
							var row1 = accessor.GetRowSpan(y + i);
							var row2 = accessor.GetRowSpan(y + i);

							for (int j = 0; j < blockWidth; j++)
							{
								var pixel1 = row1[x + j];
								var pixel2 = row2[x + j];

								int gray1 = (pixel1.R + pixel1.G + pixel1.B) / 3;
								int gray2 = (pixel2.R + pixel2.G + pixel2.B) / 3;

								hist1[gray1]++;
								hist2[gray2]++;
							}
						});
					}

					float[] normalizedHist1 = NormalizeHistogram(hist1, blockWidth * blockHeight);
					float[] normalizedHist2 = NormalizeHistogram(hist2, blockWidth * blockHeight);
					var similarity = CompareHistograms(normalizedHist1, normalizedHist2);

					results.Add((x, y, similarity));
				}
			}

			_logger.LogInformation("Сравнение блоков изображения завершено.");
			return results;
		}

		private float[] NormalizeHistogram(int[] histogram, int totalPixels)
		{
			float[] normalizedHistogram = new float[256];
			for (int i = 0; i < histogram.Length; i++)
			{
				normalizedHistogram[i] = histogram[i] / (float)totalPixels;
			}
			return normalizedHistogram;
		}

		public (double similarity, long linearTime, long parallelTime) BenchmarkComparison(Image<Rgba32> img1, Image<Rgba32> img2)
		{
			_logger.LogInformation("Начало бенчмаркинга сравнения изображений.");

			var stopwatch = System.Diagnostics.Stopwatch.StartNew();

			var hist1Linear = CalculateNormalizedHistogramSequential(img1);
			var hist2Linear = CalculateNormalizedHistogramSequential(img2);
			double linearSimilarity = CompareHistograms(hist1Linear, hist2Linear);

			stopwatch.Stop();
			long linearTime = stopwatch.ElapsedMilliseconds;

			stopwatch.Restart();

			var hist1Parallel = CalculateNormalizedHistogramParallel(img1);
			var hist2Parallel = CalculateNormalizedHistogramParallel(img2);
			double parallelSimilarity = CompareHistograms(hist1Parallel, hist2Parallel);

			stopwatch.Stop();
			long parallelTime = stopwatch.ElapsedMilliseconds;

			if (Math.Abs(linearSimilarity - parallelSimilarity) > 1e-6)
			{
				_logger.LogError("Результаты последовательного и параллельного методов значительно отличаются!");
				throw new Exception("Результаты последовательного и параллельного методов значительно отличаются!");
			}

			_logger.LogInformation("Бенчмаркинг завершен. Сравнение завершено.");
			return (parallelSimilarity, linearTime, parallelTime);
		}
	}
}
