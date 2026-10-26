using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Rotinas;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CrmObras.Processamento.Testes;

/*
 * As rotinas de limpeza, com o papel do processador — o único que executa
 * as funções que apagam por cima das construtoras. Apagam o que venceu, de
 * toda construtora, e nada mais.
 */
public sealed class RotinasTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private async Task Rodar(IRotina rotina)
    {
        await using var processamento = Processamento();
        await using var escopo = processamento.NovoEscopo();

        await rotina.Executar(escopo.ServiceProvider, Cancelamento);
    }

    private async Task GravarChave(string chave, DateTimeOffset criadaEm) =>
        await CaixaNoBanco.Executar(
            Banco,
            "insert into crm.chaves_de_idempotencia (construtora_id, chave, impressao_do_pedido, criada_em) values (@p0, @p1, 'POST /api/contas', @p2)",
            Guid.CreateVersion7(),
            chave,
            criadaEm.ToUniversalTime());

    private async Task<List<string>> ChavesQueSobraram(params string[] chaves)
    {
        await using var conexao = new NpgsqlConnection(Banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand("select chave from crm.chaves_de_idempotencia where chave = any(@p0) order by chave", conexao);
        comando.Parameters.AddWithValue("p0", chaves);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var sobraram = new List<string>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            sobraram.Add(leitor.GetString(0));
        }

        return sobraram;
    }

    [Fact]
    public async Task A_limpeza_apaga_as_chaves_de_idempotencia_com_mais_de_24_horas()
    {
        var sufixo = Guid.CreateVersion7().ToString();
        await GravarChave($"a-vencida-{sufixo}", Relogio.Agora - TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        await GravarChave($"b-no-limite-{sufixo}", Relogio.Agora - TimeSpan.FromHours(24));
        await GravarChave($"c-de-hoje-{sufixo}", Relogio.Agora - TimeSpan.FromHours(1));

        await Rodar(new LimpezaDasChavesDeIdempotencia(Relogio, NullLogger<LimpezaDasChavesDeIdempotencia>.Instance));

        Assert.Equal(
            [$"b-no-limite-{sufixo}", $"c-de-hoje-{sufixo}"],
            await ChavesQueSobraram($"a-vencida-{sufixo}", $"b-no-limite-{sufixo}", $"c-de-hoje-{sufixo}"));
    }

    [Fact]
    public async Task A_limpeza_apaga_so_as_mensagens_tratadas_ha_mais_de_sete_dias()
    {
        var agora = Relogio.Agora;
        var haUmMes = agora - TimeSpan.FromDays(30);
        var tratadaHaOito = await CaixaNoBanco.GravarCrua(Banco, Construtora, "venda-ganha", haUmMes, tratadaEm: agora - TimeSpan.FromDays(8));
        var tratadaHaSeis = await CaixaNoBanco.GravarCrua(Banco, Guid.CreateVersion7(), "venda-ganha", haUmMes, tratadaEm: agora - TimeSpan.FromDays(6));
        var pendente = await CaixaNoBanco.GravarCrua(Banco, Construtora, "venda-ganha", haUmMes);
        var abandonada = await CaixaNoBanco.GravarCrua(Banco, Construtora, "venda-ganha", haUmMes, abandonadaEm: haUmMes);

        await Rodar(new LimpezaDaCaixaDeSaida(Relogio, NullLogger<LimpezaDaCaixaDeSaida>.Instance));

        var sobraram = (await CaixaNoBanco.Todas(Banco)).Select(mensagem => mensagem.Id).ToList();

        // A pendente ainda vai ser tratada; a abandonada espera alguém ver por quê.
        Assert.DoesNotContain(tratadaHaOito, sobraram);
        Assert.Equal(new[] { tratadaHaSeis, pendente, abandonada }.Order(), sobraram.Order());
    }

    [Fact]
    public void As_limpezas_rodam_de_hora_em_hora_com_a_guarda_de_cada_uma()
    {
        Assert.Equal(
            (TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(24), TimeSpan.FromDays(7)),
            (new LimpezaDasChavesDeIdempotencia(Relogio, NullLogger<LimpezaDasChavesDeIdempotencia>.Instance).Intervalo,
             new LimpezaDaCaixaDeSaida(Relogio, NullLogger<LimpezaDaCaixaDeSaida>.Instance).Intervalo,
             LimpezaDasChavesDeIdempotencia.Validade,
             LimpezaDaCaixaDeSaida.Guarda));
    }
}
