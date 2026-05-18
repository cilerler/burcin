using System.Runtime.CompilerServices;

// Tests need to resolve internal types (INutritionFactService) to assert behaviour without
// going through the HTTP boundary. Mirror the Sourcing module — Internals stay internal at the
// production boundary; the test project alone gets visibility.
[assembly: InternalsVisibleTo("BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests")]
