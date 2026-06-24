using GlucoTrack.Shared.DTOs.Sync;

namespace GlucoTrack.Client.Services;

public static class ProductCategories
{
    public static string ImageUrl(ProductDto p) => $"/api/products/{p.Id}/image?v={p.UpdatedAtUtc.Ticks}";

    public static readonly (int Val, string Label)[] All =
    [
        (0,"Прочее"), (1,"Мясо"), (2,"Рыба"), (3,"Молочные"), (4,"Злаки"),
        (5,"Овощи"), (6,"Фрукты"), (7,"Выпечка"), (8,"Напитки"),
        (9,"Орехи"), (10,"Сладкое"), (11,"Яйца"), (12,"Бобовые"),
        (13,"Кисломолочные"), (14,"Колбасы"), (15,"Алкоголь"), (16,"Макароны"),
        (17,"Полуфабрикаты"), (18,"Грибы"), (19,"Мясо птицы"), (20,"Соусы")
    ];

    public static string Label(int cat) => cat switch
    {
        1 => "Мясо", 2 => "Рыба", 3 => "Молочные", 4 => "Злаки",
        5 => "Овощи", 6 => "Фрукты", 7 => "Выпечка", 8 => "Напитки",
        9 => "Орехи", 10 => "Сладкое", 11 => "Яйца", 12 => "Бобовые",
        13 => "Кисломолочные", 14 => "Колбасы", 15 => "Алкоголь",
        16 => "Макароны", 17 => "Полуфабрикаты", 18 => "Грибы", 19 => "Мясо птицы", 20 => "Соусы",
        _ => "Прочее"
    };

    public static string IconName(int cat) => cat switch
    {
        1  => "meat",
        2  => "fish",
        3  => "dairy",
        4  => "grains",
        5  => "vegetables",
        6  => "fruits",
        7  => "bakery",
        8  => "drinks",
        9  => "nuts",
        10 => "sweets",
        11 => "eggs",
        12 => "legumes",
        13 => "fermentedDairy",
        14 => "sausages",
        15 => "alcohol",
        16 => "pasta",
        17 => "semiFinished",
        18 => "mushrooms",
        19 => "poultry",
        20 => "sauces",
        _  => "other"
    };
}
