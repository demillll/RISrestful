using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

public class Program
{
	/// <summary>
	/// Точка входа в приложение.
	/// </summary>
	public static void Main(string[] args)
	{
		CreateHostBuilder(args).Build().Run();
	}

	/// <summary>
	/// Создает и настраивает IHostBuilder для запуска приложения.
	/// </summary>
	public static IHostBuilder CreateHostBuilder(string[] args) =>
	Host.CreateDefaultBuilder(args)
		.ConfigureWebHostDefaults(webBuilder =>
		{
			webBuilder.UseStartup<Startup>()
				.ConfigureKestrel(options =>
				{
					// Устанавливаем тайм-ауты
					options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // Время ожидания для активных соединений
					options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // Тайм-аут на заголовки запроса
					options.Limits.MaxRequestBodySize = 104857600; // Максимальный размер тела запроса (в байтах, 100MB)
				});
		});

}
