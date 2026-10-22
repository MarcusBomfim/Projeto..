using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;

namespace CrmObras.Dominio.Clientes;

/// <summary>
/// Um cliente: a empresa ou a pessoa que compra a obra, com as pessoas com
/// quem se fala lá dentro.
/// </summary>
/// <remarks>
/// <para>
/// O documento — CNPJ ou CPF — pode chegar depois: quem pede orçamento pelo
/// site raramente manda o CNPJ. Mas, informado, ele identifica a conta e não
/// muda (ADR-0002). Empresa com outro CNPJ é outra conta.
/// </para>
/// <para>
/// Havendo contatos, um deles é o principal — o primeiro, até que se escolha
/// outro. O e-mail não se repete entre os contatos da conta: dois contatos com
/// o mesmo e-mail são a mesma pessoa cadastrada duas vezes.
/// </para>
/// </remarks>
public sealed class Conta
{
    private const int MaiorNome = 200;

    private readonly List<Contato> _contatos = [];

    private ContatoId? _principal;

    public Conta(string nome, Documento? documento = null)
        : this(ContaId.Novo(), nome, documento)
    {
    }

    private Conta(ContaId id, string nome, Documento? documento)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (Textos.Problema(nome, "o nome da conta", MaiorNome) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Id = id;
        Nome = nome.Trim();
        Documento = documento;
    }

    public ContaId Id { get; }

    public string Nome { get; private set; }

    public Documento? Documento { get; private set; }

    public IReadOnlyList<Contato> Contatos => _contatos;

    /// <summary>Com quem se fala primeiro. Nulo só na conta sem contato.</summary>
    public Contato? Principal => _contatos.Find(contato => contato.Id == _principal);

    public void Renomear(string nome)
    {
        if (Textos.Problema(nome, "o nome da conta", MaiorNome) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Nome = nome.Trim();
    }

    /// <summary>Informa o documento da conta que ainda não tem um. O mesmo de novo não muda nada.</summary>
    public void InformarDocumento(Documento documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (Documento is not null && Documento != documento)
        {
            throw new ExcecaoDeDominio(
                $"A conta {Nome} já tem o documento {Documento}, e o documento identifica a conta: {documento} é de outro cliente.");
        }

        Documento = documento;
    }

    /// <summary>Adiciona uma pessoa à conta. A primeira vira a principal.</summary>
    public Contato AdicionarContato(DadosDoContato dados) => Incluir(ContatoId.Novo(), dados);

    public void AlterarContato(ContatoId id, DadosDoContato dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        var contato = Contato(id);
        Conferir(dados, alterado: contato);
        contato.Alterar(dados);
    }

    public void DefinirPrincipal(ContatoId id) => _principal = Contato(id).Id;

    /// <summary>Remove um contato. O principal só sai sozinho: havendo outros, outro é escolhido antes.</summary>
    public void RemoverContato(ContatoId id)
    {
        var contato = Contato(id);

        if (contato.Id == _principal && _contatos.Count > 1)
        {
            throw new ExcecaoDeDominio(
                $"{contato.Nome} é o contato principal da conta {Nome}: escolha outro principal antes de remover.");
        }

        _contatos.Remove(contato);

        if (_contatos.Count == 0)
        {
            _principal = null;
        }
    }

    public Contato Contato(ContatoId id) =>
        _contatos.Find(contato => contato.Id == id)
        ?? throw new ExcecaoDeDominio($"A conta {Nome} não tem o contato {id}.");

    public override string ToString() => Nome;

    /// <summary>
    /// Refaz uma conta gravada, com a identidade dela e a dos contatos (ADR-0010).
    /// Os contatos entram pela mesma conferência de quem os adicionou.
    /// </summary>
    internal static Conta Reconstituir(
        ContaId id,
        string nome,
        Documento? documento,
        IReadOnlyList<(ContatoId Id, DadosDoContato Dados)> contatos,
        ContatoId? principal)
    {
        ArgumentNullException.ThrowIfNull(contatos);

        var conta = new Conta(id, nome, documento);

        foreach (var (contato, dados) in contatos)
        {
            if (conta._contatos.Exists(incluido => incluido.Id == contato))
            {
                throw new ExcecaoDeDominio($"A conta {conta.Nome} está gravada com o contato {contato} duas vezes.");
            }

            conta.Incluir(contato, dados);
        }

        if ((principal is null) != (conta._contatos.Count == 0))
        {
            throw new ExcecaoDeDominio(principal is null
                ? $"A conta {conta.Nome} está gravada com contatos, e nenhum é o principal."
                : $"A conta {conta.Nome} está gravada sem contatos, e com o principal {principal}.");
        }

        if (principal is not null)
        {
            conta.DefinirPrincipal(principal);
        }

        return conta;
    }

    private Contato Incluir(ContatoId id, DadosDoContato dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        Conferir(dados, alterado: null);

        var contato = new Contato(id, dados);
        _contatos.Add(contato);
        _principal ??= contato.Id;

        return contato;
    }

    /// <summary>Os problemas dos dados do contato, todos de uma vez.</summary>
    private void Conferir(DadosDoContato dados, Contato? alterado)
    {
        var problemas = new List<string>();

        if (Textos.Problema(dados.Nome, "o nome do contato", MaiorNome) is { } nome)
        {
            problemas.Add(nome);
        }

        if (Textos.Opcional(dados.Cargo)?.Length > MaiorNome)
        {
            problemas.Add($"O cargo do contato passa de {MaiorNome} caracteres.");
        }

        if (dados.Email is null && dados.Telefone is null)
        {
            problemas.Add("O contato precisa de um e-mail ou de um telefone: sem um dos dois, não há como falar com ele.");
        }

        if (dados.Email is not null
            && _contatos.Find(outro => outro != alterado && outro.Email == dados.Email) is { } dono)
        {
            problemas.Add($"O e-mail {dados.Email} já é do contato {dono.Nome}.");
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }
    }
}
