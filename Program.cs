using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot;

// Класс для статистики
class TestStatistics
{
	public int TotalRequests { get; set; } = 0;
	public int SuccessfulRequests { get; set; } = 0;
	public int FailedRequests { get; set; } = 0;
}

class Program
{
	private static readonly HttpClient client = new HttpClient
	{
		Timeout = TimeSpan.FromMinutes(1) // Увеличенный таймаут
	};
	private static readonly string serverUrl = "http://192.168.43.188:7025/api/ImageComparison/compare";
	private static ILogger<Program> logger;

	// Для графиков
	private static readonly List<int> clientsPerStage = new List<int> { 50, 100, 200 };
	private static readonly List<double> durations = new List<double>();
	private static readonly List<long> memoryUsages = new List<long>();
	private static readonly List<int> threadsUsed = new List<int>();

	static async Task Main(string[] args)
	{
		Console.WriteLine("Введите количество потоков (или нажмите Enter для значений по умолчанию):");
		var input = Console.ReadLine();

		if (int.TryParse(input, out int maxThreads))
		{
			ThreadPool.SetMinThreads(maxThreads, maxThreads); // можно вручную установить мин кол-во потоков в коде здесь(100, 100)
			ThreadPool.SetMaxThreads(maxThreads, maxThreads); // можно вручную установить мин кол-во потоков в коде здесь(100, 100)
			Console.WriteLine($"Количество потоков установлено: {maxThreads}");
		}
		else
		{
			Console.WriteLine("Используются настройки по умолчанию для пула потоков.");
		}

		var host = Host.CreateDefaultBuilder(args)
			.ConfigureLogging(logging =>
			{
				logging.AddConsole();
			})
			.Build();

		logger = host.Services.GetRequiredService<ILogger<Program>>();

		logger.LogInformation("Начало тестирования.");
		var image1 = GenerateImage(500, 500, "black");
		var image2 = GenerateImage(500, 500, "white");

		foreach (var clients in clientsPerStage)
		{
			logger.LogInformation($"Этап с {clients} клиентами начался.");
			var stopwatch = Stopwatch.StartNew();

			var stageStats = new TestStatistics();

			await RunLoadTest(clients, 1, image1, image2, stageStats);

			stopwatch.Stop();
			durations.Add(stopwatch.Elapsed.TotalMilliseconds);

			memoryUsages.Add(Process.GetCurrentProcess().PrivateMemorySize64);
			threadsUsed.Add(Process.GetCurrentProcess().Threads.Count);

			logger.LogInformation($"Этап с {clients} клиентами завершён за {stopwatch.Elapsed.TotalMilliseconds} мс.");
			logger.LogInformation($"  Всего запросов: {stageStats.TotalRequests}");
			logger.LogInformation($"  Успешных запросов: {stageStats.SuccessfulRequests}");
			logger.LogInformation($"  Неудачных запросов: {stageStats.FailedRequests}");
			logger.LogInformation($"  Использование памяти: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024} МБ");
			logger.LogInformation($"  Используемые потоки: {Process.GetCurrentProcess().Threads.Count}");
		}

		logger.LogInformation("Нагрузочные тесты завершены.");
		GenerateGraphs();
	}


	static async Task RunLoadTest(int numberOfClients, int numberOfRequestsPerClient, byte[] image1, byte[] image2, TestStatistics stats)
	{
		var tasks = new Task[numberOfClients];
		for (int i = 0; i < numberOfClients; i++)
		{
			tasks[i] = SimulateClient(i, numberOfRequestsPerClient, image1, image2, stats);
		}
		await Task.WhenAll(tasks);
	}

	static async Task SimulateClient(int clientId, int numRequests, byte[] image1, byte[] image2, TestStatistics stats)
	{
		for (int i = 0; i < numRequests; i++)
		{
			using (var content = new MultipartFormDataContent())
			{
				var image1Content = new ByteArrayContent(image1);
				image1Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
				content.Add(image1Content, "image1", "image1.png");

				var image2Content = new ByteArrayContent(image2);
				image2Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
				content.Add(image2Content, "image2", "image2.png");

				lock (stats)
				{
					stats.TotalRequests++;
				}

				try
				{
					var startTime = DateTime.Now;
					var response = await client.PostAsync(serverUrl, content);
					var duration = DateTime.Now - startTime;

					if (response.IsSuccessStatusCode)
					{
						lock (stats)
						{
							stats.SuccessfulRequests++;
						}
						logger.LogInformation($"Клиент {clientId}, запрос {i + 1}: Успех. Время отклика: {duration.TotalMilliseconds} мс.");
					}
					else
					{
						lock (stats)
						{
							stats.FailedRequests++;
						}
						logger.LogError($"Клиент {clientId}, запрос {i + 1}: Неудача. Код ответа: {response.StatusCode}. Время отклика: {duration.TotalMilliseconds} мс.");
					}
				}
				catch (Exception ex)
				{
					lock (stats)
					{
						stats.FailedRequests++;
					}
					logger.LogError($"Клиент {clientId}, запрос {i + 1}: Ошибка - {ex.Message}");
				}
			}
			// Задержка между запросами
			await Task.Delay(10); // 10 миллисекунд паузы между запросами
		}
	}

	static byte[] GenerateImage(int width, int height, string color)
	{
		using (var image = new Image<Rgba32>(width, height))
		{
			var colorValue = color.ToLower() == "black" ? new Rgba32(0, 0, 0) : new Rgba32(255, 255, 255);
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					image[x, y] = colorValue;
				}
			}
			using (var ms = new MemoryStream())
			{
				image.SaveAsPng(ms);
				return ms.ToArray();
			}
		}
	}

	static void GenerateGraphs()
	{
		string outputDirectory = "D:/ycheba/4kyrs7sem/RIS/Kyrsach/KyrsachC/Graphs";
		Directory.CreateDirectory(outputDirectory);

		// График времени выполнения
		var plt1 = new Plot();
		plt1.AddScatter(clientsPerStage.Select(x => (double)x).ToArray(), durations.ToArray(),
			color: System.Drawing.Color.Blue, lineWidth: 2, markerSize: 10);
		plt1.Title("Время выполнения в зависимости от количества клиентов", size: 16);
		plt1.XAxis.Label("Количество клиентов");
		plt1.YAxis.Label("Время (мс)");
		plt1.XAxis.TickLabelStyle(fontSize: 12);
		plt1.YAxis.TickLabelStyle(fontSize: 12);
		plt1.Grid(enable: true);
		plt1.SaveFig(Path.Combine(outputDirectory, "TimeVsClients.png"));

		// График использования памяти
		var plt2 = new Plot();
		plt2.AddScatter(clientsPerStage.Select(x => (double)x).ToArray(), memoryUsages.Select(x => x / 1024.0 / 1024).ToArray(),
			color: System.Drawing.Color.Red, lineWidth: 2, markerSize: 10);
		plt2.Title("Использование памяти в зависимости от количества клиентов", size: 16);
		plt2.XAxis.Label("Количество клиентов");
		plt2.YAxis.Label("Память (МБ)");
		plt2.XAxis.TickLabelStyle(fontSize: 12);
		plt2.YAxis.TickLabelStyle(fontSize: 12);
		plt2.Grid(enable: true);
		plt2.SaveFig(Path.Combine(outputDirectory, "MemoryVsClients.png"));

		// График использования потоков
		var plt3 = new Plot();
		plt3.AddScatter(clientsPerStage.Select(x => (double)x).ToArray(), threadsUsed.Select(x => (double)x).ToArray(),
			color: System.Drawing.Color.Green, lineWidth: 2, markerSize: 10);
		plt3.Title("Использование потоков в зависимости от количества клиентов", size: 16);
		plt3.XAxis.Label("Количество клиентов");
		plt3.YAxis.Label("Потоки");
		plt3.XAxis.TickLabelStyle(fontSize: 12);
		plt3.YAxis.TickLabelStyle(fontSize: 12);
		plt3.Grid(enable: true);
		plt3.SaveFig(Path.Combine(outputDirectory, "ThreadsVsClients.png"));

		logger.LogInformation($"Графики сохранены в директории: {outputDirectory}");
	}


}
