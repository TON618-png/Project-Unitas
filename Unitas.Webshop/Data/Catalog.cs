namespace Unitas.Webshop.Data;

public sealed record Product(string Id, string Name, string Type, string Description, string Capacity,
    string Specification, string Accent, string Question, string Answer, string[] Manual, decimal Price);

public static class Catalog
{
    public static IReadOnlyList<Product> Products { get; } =
    [
        new("ubs-oven-60", "Rotatieoven RO 60", "Ovens",
            "Gelijkmatig bakken op meerdere niveaus, van knapperige broden tot luchtige viennoiserie.",
            "Wagen voor 18 bakplaten", "Bakplaten 600 × 800 mm", "oven",
            "Kan ik stoom toevoegen tijdens het bakken?",
            "Vraag bij een offerte naar de uitvoering met stoominjectie.",
            ["Controleer de ovenruimte en de bakwagen.", "Kies een programma en laat de oven voorverwarmen.",
             "Volg de fabrikantvoorschriften voor het laden en reinigen."], 18950m),
        new("ubs-mix-80", "Spiraalkneder SK 80", "Deegbereiding",
            "Kneed brood- en pizzadeeg in een roestvrijstalen kuip met twee snelheden.",
            "Tot 80 kg deeg per batch", "Twee kneedsnelheden", "mixer",
            "Kan ik kleine batches kneden?",
            "Het minimale batchgewicht hangt af van het recept. Vraag een proefbatch aan.",
            ["Controleer de kuip en veiligheidskap.", "Doseer de ingrediënten binnen de machinecapaciteit.",
             "Sluit de veiligheidskap en volg de fabrikantvoorschriften."], 6495m),
        new("ubs-deeg-400", "Deegverdeler DV 400", "Portioneren",
            "Verdeel deeg in consistente porties voor een gelijkmatige productielijn.",
            "Tot 400 porties per uur", "Instelbaar portiegewicht", "divider",
            "Hoe controleer ik het portiegewicht?",
            "Weeg na een proefronde meerdere porties en vergelijk ze met het gewenste gewicht.",
            ["Controleer de invoer en uitvoer.", "Stel het gewenste gewicht in en voer een proefronde uit.",
             "Weeg enkele porties en stel indien nodig bij."], 9250m)
    ];

    public static Product? Find(string id) => Products.FirstOrDefault(product => product.Id == id);
}
