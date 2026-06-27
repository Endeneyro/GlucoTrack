namespace GlucoTrack.Shared.Entities;

/// <summary>
/// Препарат (бренд) болюсного инсулина. Задаёт пресет времени пика активности
/// для экспоненциальной модели IOB. <see cref="Other"/> — прочий/ручная настройка.
/// </summary>
public enum InsulinBrand
{
    Other = 0,
    Fiasp = 1,
    Lyumjev = 2,
    Humalog = 3,
    NovoRapid = 4,
    Apidra = 5,
}
