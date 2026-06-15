using GlucoTrack.Shared.Events;

namespace GlucoTrack.Shared.Tests;

public class NutritionMathTests
{
    [Fact]
    public void ByWeight_GramsUnchanged()
    {
        Assert.Equal(150.0, NutritionMath.EffectiveGrams(0, 150, pieceWeightG: null));
        Assert.Equal(150.0, NutritionMath.EffectiveGrams(0, 150, pieceWeightG: 25)); // вес штуки игнорируется для весовых
    }

    [Fact]
    public void ByPiece_MultipliesByPieceWeight()
    {
        // 2 куска × 25 г = 50 г
        Assert.Equal(50.0, NutritionMath.EffectiveGrams(1, 2, pieceWeightG: 25));
    }

    [Fact]
    public void ByPiece_NoPieceWeight_DefaultsTo100()
    {
        Assert.Equal(300.0, NutritionMath.EffectiveGrams(1, 3, pieceWeightG: null));
    }

    [Fact]
    public void ScaleFactor_DividesBy100()
    {
        Assert.Equal(0.25, NutritionMath.ScaleFactor(25));
        Assert.Equal(1.5, NutritionMath.ScaleFactor(150));
    }

    [Fact]
    public void Zero_Grams_Zero()
    {
        Assert.Equal(0.0, NutritionMath.EffectiveGrams(1, 0, 25));
        Assert.Equal(0.0, NutritionMath.EffectiveGrams(0, 0, null));
    }
}
