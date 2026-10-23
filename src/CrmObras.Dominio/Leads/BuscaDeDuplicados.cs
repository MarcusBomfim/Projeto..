using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Leads;

/// <summary>Por que uma conta parece ser o cliente do lead — da semelhança mais forte para a mais fraca.</summary>
public enum Semelhanca
{
    /// <summary>O mesmo CNPJ ou CPF: é o mesmo cliente, sem dúvida.</summary>
    MesmoDocumento,

    /// <summary>CNPJ com a mesma raiz: a matriz e uma filial, ou duas filiais, da mesma empresa.</summary>
    MesmaEmpresa,

    /// <summary>O e-mail do lead já é de um contato da conta.</summary>
    MesmoEmail,

    /// <summary>O telefone do lead já é de um contato da conta.</summary>
    MesmoTelefone,

    /// <summary>O nome da empresa é o mesmo, sem contar acento, maiúscula e "Ltda".</summary>
    MesmoNome,

    /// <summary>O e-mail é do mesmo domínio de um contato da conta — alguém da mesma empresa.</summary>
    MesmoDominio,
}

/// <summary>Uma razão para achar que a conta é o cliente do lead, numa frase.</summary>
public sealed record Indicio(Semelhanca Semelhanca, string Motivo);

/// <summary>Uma conta que pode ser o cliente do lead, com os indícios, o mais forte primeiro.</summary>
public sealed record PossivelDuplicado(ContaId Conta, string NomeDaConta, IReadOnlyList<Indicio> Indicios)
{
    public Semelhanca MaisForte => Indicios[0].Semelhanca;
}

/// <summary>
/// Procura, entre as contas, o cliente que o lead talvez já seja (ADR-0003).
/// </summary>
/// <remarks>
/// <para>
/// Só o mesmo documento é certeza. O resto é indício, para quem converte
/// decidir: o mesmo e-mail pode ser de um arquiteto que atende duas
/// construtoras; o mesmo nome, de duas empresas homônimas em cidades
/// diferentes.
/// </para>
/// <para>
/// O domínio de e-mail só conta quando é de empresa: dois clientes com
/// endereço no Gmail não têm nada em comum.
/// </para>
/// </remarks>
public static class BuscaDeDuplicados
{
    private static readonly FrozenSet<string> ProvedoresGratuitos = new[]
    {
        "gmail.com", "googlemail.com", "hotmail.com", "hotmail.com.br", "outlook.com", "outlook.com.br", "live.com",
        "msn.com", "yahoo.com", "yahoo.com.br", "icloud.com", "me.com", "uol.com.br", "bol.com.br", "terra.com.br",
        "ig.com.br", "globo.com", "globomail.com", "protonmail.com", "proton.me",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>As palavras que não distinguem uma empresa: a natureza jurídica e os conectivos.</summary>
    private static readonly FrozenSet<string> PalavrasSemPeso = new[]
    {
        "ltda", "sa", "me", "epp", "eireli", "mei", "slu", "cia", "de", "da", "do", "das", "dos", "e",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlyList<PossivelDuplicado> Do(Lead lead, IEnumerable<Conta> contas)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(contas);

        var nomeDoLead = NomeComparavel(lead.NomeDoCliente);

        return [.. contas
            .Select(conta => (conta, indicios: Indicios(lead, nomeDoLead, conta).OrderBy(indicio => indicio.Semelhanca).ToList()))
            .Where(par => par.indicios.Count > 0)
            .Select(par => new PossivelDuplicado(par.conta.Id, par.conta.Nome, par.indicios))
            .OrderBy(duplicado => duplicado.MaisForte)
            .ThenBy(duplicado => duplicado.NomeDaConta, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true))];
    }

    /// <summary>Se o e-mail é de um domínio de empresa — e não de um provedor gratuito, que não aproxima ninguém.</summary>
    public static bool EhDeEmpresa(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return !ProvedoresGratuitos.Contains(email.Dominio);
    }

    /// <summary>
    /// O nome como se compara: sem acento, em minúsculas, sem pontuação e sem
    /// as palavras que não distinguem uma empresa. "Construtora Alfa Ltda." e
    /// "CONSTRUTORA ALFA" dão o mesmo.
    /// </summary>
    public static string NomeComparavel(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        var semAcento = new StringBuilder();

        foreach (var caractere in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(char.IsAsciiLetterOrDigit(caractere) ? char.ToLowerInvariant(caractere) : ' ');
            }
        }

        var palavras = semAcento.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', JuntarSa(palavras).Where(palavra => !PalavrasSemPeso.Contains(palavra)));
    }

    /// <summary>"S/A" e "S.A." viram as palavras "s" e "a": juntas de novo, somem com as outras palavras sem peso.</summary>
    private static IEnumerable<string> JuntarSa(string[] palavras)
    {
        for (var posicao = 0; posicao < palavras.Length; posicao++)
        {
            if (palavras[posicao] == "s" && posicao + 1 < palavras.Length && palavras[posicao + 1] == "a")
            {
                yield return "sa";
                posicao++;
            }
            else
            {
                yield return palavras[posicao];
            }
        }
    }

    private static IEnumerable<Indicio> Indicios(Lead lead, string nomeDoLead, Conta conta)
    {
        if (lead.Documento is not null && conta.Documento is not null)
        {
            if (lead.Documento == conta.Documento)
            {
                yield return new Indicio(Semelhanca.MesmoDocumento, $"mesmo documento, {conta.Documento}");
            }
            else if (lead.Documento is Cnpj doLead && conta.Documento is Cnpj daConta && doLead.Raiz == daConta.Raiz)
            {
                yield return new Indicio(Semelhanca.MesmaEmpresa, $"a mesma empresa: {daConta} tem a raiz do CNPJ do lead");
            }
        }

        if (lead.Email is { } email && conta.Contatos.FirstOrDefault(contato => contato.Email == email) is { } doEmail)
        {
            yield return new Indicio(Semelhanca.MesmoEmail, $"o e-mail {email} é de {doEmail.Nome}");
        }
        else if (lead.Email is { } corporativo
            && EhDeEmpresa(corporativo)
            && conta.Contatos.FirstOrDefault(contato => contato.Email?.Dominio == corporativo.Dominio) is { } colega)
        {
            yield return new Indicio(Semelhanca.MesmoDominio, $"{colega.Nome} também tem e-mail em {corporativo.Dominio}");
        }

        if (lead.Telefone is { } telefone && conta.Contatos.FirstOrDefault(contato => contato.Telefone == telefone) is { } doTelefone)
        {
            yield return new Indicio(Semelhanca.MesmoTelefone, $"o telefone {telefone} é de {doTelefone.Nome}");
        }

        if (nomeDoLead.Length > 0 && NomeComparavel(conta.Nome) == nomeDoLead)
        {
            yield return new Indicio(Semelhanca.MesmoNome, $"o mesmo nome, {conta.Nome}");
        }
    }
}
