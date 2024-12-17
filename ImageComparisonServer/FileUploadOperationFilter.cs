using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class FileUploadOperationFilter : IOperationFilter
{
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var fileParameters = context.MethodInfo.GetParameters()
			.Where(p => p.ParameterType == typeof(IFormFile));

		foreach (var parameter in fileParameters)
		{
			operation.RequestBody = new OpenApiRequestBody
			{
				Content = new Dictionary<string, OpenApiMediaType>
				{
					["multipart/form-data"] = new OpenApiMediaType
					{
						Schema = new OpenApiSchema
						{
							Type = "object",
							Properties =
							{
								[parameter.Name] = new OpenApiSchema
								{
									Type = "string",
									Format = "binary"
								}
							},
							Required = new HashSet<string> { parameter.Name }
						}
					}
				}
			};
		}
	}
}
