using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using ImageComparisonServer.Services;
using Microsoft.Extensions.Logging;

[Route("api/[controller]")]
[ApiController]
public class ImageComparisonController : ControllerBase
{
	private readonly IImageComparisonService _imageComparisonService;
	private readonly ILogger<ImageComparisonController> _logger;
	private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

	public ImageComparisonController(IImageComparisonService imageComparisonService, ILogger<ImageComparisonController> logger)
	{
		_imageComparisonService = imageComparisonService;
		_logger = logger;
	}
		
	private IActionResult ValidateImages(IFormFile image1, IFormFile image2)
	{
		if (image1 == null || image2 == null)
		{
			_logger.LogWarning("Одно или оба изображения не были загружены.");
			return BadRequest("Оба изображения должны быть загружены.");
		}

		if (!image1.ContentType.StartsWith("image/") || !image2.ContentType.StartsWith("image/"))
		{
			_logger.LogWarning("Загруженные файлы не являются изображениями.");
			return BadRequest("Загружаемые файлы должны быть изображениями.");
		}

		if (image1.Length > MaxFileSize || image2.Length > MaxFileSize)
		{
			_logger.LogWarning($"Размер изображения превышает допустимый лимит: {MaxFileSize / 1024 / 1024} MB.");
			return BadRequest($"Размер каждого изображения не должен превышать {MaxFileSize / 1024 / 1024} MB.");
		}

		return null; // Все проверки пройдены
	}

	[HttpPost("compare")]
	public async Task<IActionResult> CompareImages([FromForm] IFormFile image1, [FromForm] IFormFile image2)
	{
		var validationError = ValidateImages(image1, image2);
		if (validationError != null)
			return validationError;

		try
		{
			_logger.LogInformation("Начало сравнения изображений (последовательный метод).");

			await using var img1Stream = image1.OpenReadStream();
			await using var img2Stream = image2.OpenReadStream();

			using var img1 = await Image.LoadAsync<Rgba32>(img1Stream);
			using var img2 = await Image.LoadAsync<Rgba32>(img2Stream);

			_logger.LogInformation("Изображения загружены успешно, начинается обработка.");

			var hist1 = _imageComparisonService.CalculateNormalizedHistogramSequential(img1);
			var hist2 = _imageComparisonService.CalculateNormalizedHistogramSequential(img2);

			var similarity = _imageComparisonService.CompareHistograms(hist1, hist2);

			_logger.LogInformation("Сравнение изображений завершено. Схожесть: {Similarity}%", similarity);

			return Ok(new { similarity });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Произошла ошибка при обработке изображений.");
			return StatusCode(500, $"Произошла ошибка при обработке изображений: {ex.Message}");
		}
	}

	[HttpPost("compare-with-blocks")]
	public async Task<IActionResult> CompareWithBlocks([FromForm] IFormFile image1, [FromForm] IFormFile image2)
	{
		var validationError = ValidateImages(image1, image2);
		if (validationError != null)
			return validationError;

		try
		{
			_logger.LogInformation("Начало сравнения блоков изображений.");

			await using var img1Stream = image1.OpenReadStream();
			await using var img2Stream = image2.OpenReadStream();

			using var img1 = await Image.LoadAsync<Rgba32>(img1Stream);
			using var img2 = await Image.LoadAsync<Rgba32>(img2Stream);

			if (img1.Size != img2.Size)
			{
				_logger.LogWarning("Изображения имеют разные размеры.");
				return BadRequest("Изображения должны быть одинакового размера для подсчёта схожих блоков.");
			}

			var blockResults = _imageComparisonService.CompareImageBlocks(img1, img2);

			_logger.LogInformation("Сравнение блоков завершено.");

			return Ok(blockResults);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Произошла ошибка при обработке изображений для сравнения блоков.");
			return StatusCode(500, $"Произошла ошибка при обработке изображений: {ex.Message}");
		}
	}

	[HttpPost("benchmark")]
	public async Task<IActionResult> Benchmark([FromForm] IFormFile image1, [FromForm] IFormFile image2)
	{
		var validationError = ValidateImages(image1, image2);
		if (validationError != null)
			return validationError;

		try
		{
			_logger.LogInformation("Начало бенчмаркинга изображений.");

			await using var img1Stream = image1.OpenReadStream();
			await using var img2Stream = image2.OpenReadStream();

			using var img1 = await Image.LoadAsync<Rgba32>(img1Stream);
			using var img2 = await Image.LoadAsync<Rgba32>(img2Stream);

			var (similarity, linearTime, parallelTime) = _imageComparisonService.BenchmarkComparison(img1, img2);

			_logger.LogInformation("Бенчмаркинг завершен. Схожесть: {Similarity}%, Время (линейное): {LinearTime} мс, Время (параллельное): {ParallelTime} мс", similarity, linearTime, parallelTime);

			return Ok(new
			{
				similarity,
				linearTime,
				parallelTime
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Произошла ошибка при выполнении бенчмарка.");
			return StatusCode(500, $"Произошла ошибка при выполнении бенчмарка: {ex.Message}");
		}
	}
}
