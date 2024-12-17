using ImageComparisonServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddControllers();
		services.AddScoped<IImageComparisonService, ImageComparisonService>(); // Регистрация сервиса
		services.AddEndpointsApiExplorer();
		services.AddCors(options =>
		{
			options.AddPolicy("AllowAll", builder =>
			{
				builder.AllowAnyOrigin()
					   .AllowAnyMethod()
					   .AllowAnyHeader();
			});
		});
	}

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.UseDefaultFiles(); // Для поиска index.html
		app.UseStaticFiles();  // Для обслуживания статических файлов

		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler(errorApp =>
			{
				errorApp.Run(async context =>
				{
					context.Response.StatusCode = 500;
					context.Response.ContentType = "application/json";
					await context.Response.WriteAsync("Произошла внутренняя ошибка сервера.");
				});
			});
		}

		app.UseRouting();
		app.UseCors("AllowAll"); // Включаем CORS
		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
		});
	}
}
