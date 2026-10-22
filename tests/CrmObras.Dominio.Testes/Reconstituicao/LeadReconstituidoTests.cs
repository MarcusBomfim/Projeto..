using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Testes.Leads;
using static CrmObras.Dominio.Testes.Leads.LeadsDeTeste;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class LeadReconstituidoTests
{
    public static TheoryData<string> Situacoes => ["novo", "em qualificação", "qualificado", "descartado sem avaliação", "descartado depois de qualificado", "reaberto", "convertido"];

    [Theory]
    [MemberData(nameof(Situacoes))]
    public void Volta_na_situacao_em_que_estava(string situacao)
    {
        var lead = NaSituacao(situacao);

        var refeito = Refazer(lead, lead.Estado);

        Assert.Equal(lead.Estado, refeito.Estado);
        Assert.Equal(
            (lead.Id, lead.Nome, lead.Empresa, lead.Email, lead.Telefone, lead.Interesse, lead.Origem, lead.DetalheDaOrigem, lead.ChegouEm),
            (refeito.Id, refeito.Nome, refeito.Empresa, refeito.Email, refeito.Telefone, refeito.Interesse, refeito.Origem, refeito.DetalheDaOrigem, refeito.ChegouEm));
    }

    [Fact]
    public void Qualificado_sem_a_avaliacao_que_qualifica_e_recusado_pela_regra_de_sempre()
    {
        var lead = DoSite();
        var naoQualifica = new Avaliacao(Resposta.Sim, Resposta.Nao, Resposta.Sim, Resposta.NaoSabido);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(lead, new EstadoDoLead(SituacaoDoLead.Qualificado, naoQualifica, QualificadoEm: Segunda)));

        Assert.Equal("O lead Marina Costa ainda não se qualifica: falta falar com quem decide ou ter o prazo da obra.", erro.Message);
    }

    [Fact]
    public void Descarte_por_outro_motivo_sem_observacao_e_recusado_pela_regra_de_sempre()
    {
        var lead = DoSite();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(
            lead,
            new EstadoDoLead(SituacaoDoLead.Descartado, Avaliacao.NaoFeita, MotivoDoDescarte: MotivoDoDescarte.Outro, DescartadoEm: Segunda)));

        Assert.Equal("Descarte por outro motivo diz qual, na observação.", erro.Message);
    }

    private static readonly Dictionary<string, EstadoDoLead> EstadosIncoerentes = new()
    {
        ["novo, mas avaliado"] = new(SituacaoDoLead.Novo, QueQualifica),
        ["em qualificação, com data de qualificado"] = new(SituacaoDoLead.EmQualificacao, QueQualifica, QualificadoEm: Segunda),
        ["qualificado, sem a data"] = new(SituacaoDoLead.Qualificado, QueQualifica),
        ["descartado, sem o motivo"] = new(SituacaoDoLead.Descartado, QueQualifica, DescartadoEm: Segunda),
        ["descartado, e qualificado"] = new(SituacaoDoLead.Descartado, QueQualifica, Segunda, MotivoDoDescarte.SemOrcamento, DescartadoEm: Segunda),
        ["convertido, sem a conta"] = new(SituacaoDoLead.Convertido, QueQualifica, Segunda, Contato: ContatoId.Novo(), ConvertidoEm: Segunda),
        ["em qualificação, com conta"] = new(SituacaoDoLead.EmQualificacao, QueQualifica, Conta: ContaId.Novo()),
    };

    public static TheoryData<string> Incoerentes => [.. EstadosIncoerentes.Keys];

    [Theory]
    [MemberData(nameof(Incoerentes))]
    public void O_estado_que_as_regras_nao_produzem_e_recusado(string caso)
    {
        var estado = EstadosIncoerentes[caso];

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(DoSite(), estado));

        Assert.True(erro.Message.EndsWith("num estado que as regras não produzem.", StringComparison.Ordinal), $"{caso}: {erro.Message}");
        Assert.StartsWith($"O lead Marina Costa está gravado como {estado.Situacao}", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Os_dados_passam_pela_regra_de_quem_recebe_o_lead()
    {
        var lead = DoSite();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Lead.Reconstituir(
            lead.Id,
            DoGalpao() with { Nome = "", Email = null, Telefone = null },
            OrigemDoLead.Site,
            null,
            Segunda,
            lead.Estado));

        Assert.Equal("Falta o nome de quem procurou. O lead precisa de um e-mail ou de um telefone: sem um dos dois, não há como responder.", erro.Message);
    }

    private static Lead Refazer(Lead lead, EstadoDoLead estado) =>
        Lead.Reconstituir(
            lead.Id,
            new DadosDoLead(lead.Nome, lead.Empresa, lead.Documento, lead.Email, lead.Telefone, lead.Interesse),
            lead.Origem,
            lead.DetalheDaOrigem,
            lead.ChegouEm,
            estado);

    private static Lead NaSituacao(string situacao)
    {
        var lead = DoSite();

        switch (situacao)
        {
            case "em qualificação":
                lead.Avaliar(QueQualifica with { Prazo = Resposta.Nao });
                break;
            case "qualificado":
                lead = Qualificado();
                break;
            case "descartado sem avaliação":
                lead.Descartar(MotivoDoDescarte.Outro, "Só queria o preço do metro quadrado", Segunda.AddDays(1));
                break;
            case "descartado depois de qualificado":
                lead = Qualificado();
                lead.Descartar(MotivoDoDescarte.PrazoDistante, null, Segunda.AddDays(3));
                break;
            case "reaberto":
                lead = Qualificado();
                lead.Descartar(MotivoDoDescarte.NaoResponde, null, Segunda.AddDays(3));
                lead.Reabrir();
                break;
            case "convertido":
                lead = Qualificado();
                lead.ConverterEmContaNova([], Segunda.AddDays(4));
                break;
        }

        return lead;
    }
}
