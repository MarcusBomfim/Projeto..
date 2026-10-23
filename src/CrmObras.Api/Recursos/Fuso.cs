namespace CrmObras.Api.Recursos;

/// <summary>
/// A hora da construtora. O servidor conta o tempo em UTC; a construtora
/// vive num fuso — e é nele que se diz o dia: o "hoje" da agenda, o último
/// dia da validade da proposta, os dias de um relatório (ADR-0014).
/// </summary>
/// <remarks>
/// Os instantes que a API dá ao domínio saem daqui, no fuso da construtora;
/// gravados, viram UTC, e o instante é o mesmo.
/// </remarks>
public sealed class Fuso(TimeZoneInfo zona, TimeProvider relogio)
{
    /// <summary>O fuso da instalação, sem a configuração <c>Fuso</c>.</summary>
    public const string Padrao = "America/Sao_Paulo";

    /// <summary>Agora, na hora da construtora.</summary>
    public DateTimeOffset Agora() => TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), zona);

    /// <summary>Agora, noutro fuso: o de quem olha a agenda de longe.</summary>
    public DateTimeOffset AgoraEm(TimeZoneInfo outro) => TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), outro);

    /// <summary>Um fuso pelo nome da base IANA — <c>America/Sao_Paulo</c> —, ou nulo, se não há fuso com o nome.</summary>
    public static TimeZoneInfo? Ler(string nome) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(nome, out var encontrado) ? encontrado : null;
}
