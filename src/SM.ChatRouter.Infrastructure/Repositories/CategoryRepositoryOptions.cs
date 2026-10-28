namespace SM.ChatRouter.Infrastructure.Repositories;

public sealed class CategoryRepositoryOptions
{
    public const string SectionName = "Categories";

    /// <summary>Ruta al JSON de categorías. Si es relativa, se resuelve contra el directorio de la aplicación.</summary>
    public string FilePath { get; set; } = "Data/categories_tree.json";
}
