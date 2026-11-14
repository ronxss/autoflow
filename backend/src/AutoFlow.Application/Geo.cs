namespace AutoFlow.Application;

public static class Geo
{
    private const double RaioTerraKm = 6371.0;

    /// <summary>Distância em km entre dois pontos (fórmula de Haversine).</summary>
    public static double DistanciaKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = Radianos(lat2 - lat1);
        var dLon = Radianos(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Radianos(lat1)) * Math.Cos(Radianos(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return RaioTerraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Radianos(double graus) => graus * Math.PI / 180.0;
}

/// <summary>A grade de atendimento é definida no horário de Brasília; as datas trafegam em UTC.</summary>
public static class FusoHorario
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTime ParaBrasilia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia);
}
