using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Propostas;
using static CrmObras.Dominio.Testes.Propostas.PropostasDeTeste;

namespace CrmObras.Dominio.Testes.Propostas;

public sealed class RespostaDoClienteTests
{
    private static readonly DateTimeOffset UmaSemanaDepois = Segunda.AddDays(8);

    /// <summary>O último instante de validade: 30/11/2026, 23h59, em Brasília.</summary>
    private static readonly DateTimeOffset UltimoMinuto = new(2026, 11, 30, 23, 59, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void O_cliente_aceita_a_versao_que_esta_com_ele()
    {
        var proposta = Enviada();

        proposta.Aceitar(UmaSemanaDepois);

        Assert.Same(proposta.Atual, proposta.Aceita);
        Assert.Equal((SituacaoDaVersao.Aceita, (DateTimeOffset?)UmaSemanaDepois), (proposta.Atual.Situacao, proposta.Atual.RespondidaEm));
        Assert.Null(proposta.Vigente);
    }

    [Fact]
    public void Aceita_ate_o_ultimo_dia_da_validade()
    {
        var proposta = Enviada();

        Assert.False(proposta.EstaVencida(UltimoMinuto));
        Assert.Same(proposta.Vigente, proposta.ConferirAceite(UltimoMinuto));
        proposta.Aceitar(UltimoMinuto);

        Assert.NotNull(proposta.Aceita);
    }

    [Fact]
    public void Vencida_nao_se_aceita()
    {
        var proposta = Enviada();
        var vencida = UltimoMinuto.AddMinutes(1);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Aceitar(vencida));

        Assert.True(proposta.EstaVencida(vencida));
        Assert.Equal("A versão 1 venceu em 30/11/2026: para o cliente aceitar, envie uma versão nova.", erro.Message);
        Assert.Null(proposta.Aceita);
    }

    [Fact]
    public void Sem_versao_com_o_cliente_nada_vence()
    {
        var proposta = DoGalpao(Venda());

        Assert.False(proposta.EstaVencida(UltimoMinuto.AddYears(1)));
    }

    [Fact]
    public void Conferir_o_aceite_nao_muda_nada()
    {
        var proposta = Enviada();

        var versao = proposta.ConferirAceite(UmaSemanaDepois);

        Assert.Equal((SituacaoDaVersao.Enviada, (VersaoDaProposta?)null), (versao.Situacao, proposta.Aceita));
    }

    [Fact]
    public void Aceitar_descarta_o_rascunho_que_estava_sendo_feito()
    {
        var proposta = Enviada(5m);
        var primeira = proposta.Atual;
        proposta.NovaVersao();

        proposta.Aceitar(UmaSemanaDepois);

        Assert.Equal([primeira], proposta.Versoes);
        Assert.Same(primeira, proposta.Aceita);
    }

    [Fact]
    public void A_aceita_nao_muda_mais()
    {
        var proposta = Enviada();
        proposta.Aceitar(UmaSemanaDepois);
        var aceita = "A proposta foi aceita na versão 1, e não muda mais.";

        Assert.Equal(aceita, Assert.Throws<ExcecaoDeDominio>(() => proposta.NovaVersao()).Message);
        Assert.Equal(aceita, Assert.Throws<ExcecaoDeDominio>(() => proposta.Redigir(DoGalpao())).Message);
        Assert.Equal(aceita, Assert.Throws<ExcecaoDeDominio>(() => proposta.Aceitar(UmaSemanaDepois)).Message);
        Assert.Equal(aceita, Assert.Throws<ExcecaoDeDominio>(() => proposta.Recusar("Mudou de ideia", UmaSemanaDepois)).Message);
        Assert.Equal(aceita, Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, Alcadas.Padrao, UmaSemanaDepois)).Message);
    }

    [Fact]
    public void Sem_versao_com_o_cliente_nao_ha_o_que_responder()
    {
        var proposta = DoGalpao(Venda());

        var aceitar = Assert.Throws<ExcecaoDeDominio>(() => proposta.Aceitar(UmaSemanaDepois));
        var recusar = Assert.Throws<ExcecaoDeDominio>(() => proposta.Recusar("Caro", UmaSemanaDepois));

        Assert.Equal("Nenhuma versão está com o cliente: não há o que aceitar.", aceitar.Message);
        Assert.Equal("Nenhuma versão está com o cliente: não há o que recusar.", recusar.Message);
    }

    [Fact]
    public void A_recusa_diz_o_motivo_e_a_versao_nova_responde()
    {
        var proposta = Enviada();
        var primeira = proposta.Atual;

        var semMotivo = Assert.Throws<ExcecaoDeDominio>(() => proposta.Recusar(" ", UmaSemanaDepois));
        var longo = Assert.Throws<ExcecaoDeDominio>(() => proposta.Recusar(new string('m', 1001), UmaSemanaDepois));
        proposta.Recusar("  Acima do orçamento aprovado pela diretoria ", UmaSemanaDepois);
        var naoHaMais = Assert.Throws<ExcecaoDeDominio>(() => proposta.Aceitar(UmaSemanaDepois));
        var segunda = proposta.NovaVersao();

        Assert.Equal("A recusa da versão 1 diz o motivo: é o que a versão nova vai responder.", semMotivo.Message);
        Assert.Equal("O motivo da recusa passa de 1000 caracteres.", longo.Message);
        Assert.Equal(
            (SituacaoDaVersao.Recusada, "Acima do orçamento aprovado pela diretoria", (DateTimeOffset?)UmaSemanaDepois),
            (primeira.Situacao, primeira.MotivoDaRecusa, primeira.RespondidaEm));
        Assert.Equal("Nenhuma versão está com o cliente: não há o que aceitar.", naoHaMais.Message);
        Assert.Equal((2, SituacaoDaVersao.Rascunho), (segunda.Numero, segunda.Situacao));
    }

    [Fact]
    public void A_recusa_aceita_o_motivo_no_limite()
    {
        var proposta = Enviada();

        proposta.Recusar(new string('m', 1000), UmaSemanaDepois);

        Assert.Equal(1000, proposta.Atual.MotivoDaRecusa?.Length);
    }

    [Fact]
    public void A_recusada_continua_recusada_quando_a_nova_e_enviada()
    {
        var proposta = Enviada();
        var primeira = proposta.Atual;
        proposta.Recusar("Caro", UmaSemanaDepois);
        proposta.NovaVersao();

        proposta.Enviar(Carlos, Alcadas.Padrao, UmaSemanaDepois.AddDays(1));

        Assert.Equal(SituacaoDaVersao.Recusada, primeira.Situacao);
        Assert.Same(proposta.Atual, proposta.Vigente);
    }
}
