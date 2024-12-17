/// <summary>
/// Модель для загрузки двух изображений для сравнения.
/// </summary>
public class ImageUploadModel
{
	/// <summary>
	/// Первое изображение для сравнения.
	/// </summary>
	public IFormFile Image1 { get; set; }

	/// <summary>
	/// Второе изображение для сравнения.
	/// </summary>
	public IFormFile Image2 { get; set; }
}
