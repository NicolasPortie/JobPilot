using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class Camada2_SessionContextTests
{
    [Fact]
    public void WasValueUsed_NovoValor_NaoUsado()
    {
        var ctx = new SessionContext();
        var used = ctx.WasValueUsed("Nicolas", SemanticFieldType.FullName);
        Assert.False(used);
    }

    [Fact]
    public void WasValueUsed_ValorJaUsado_MesmoTipo_NaoBloqueia()
    {
        var ctx = new SessionContext();
        ctx.Record("field1", "Nicolas", SemanticFieldType.FullName);

        var used = ctx.WasValueUsed("Nicolas", SemanticFieldType.FullName);
        Assert.False(used);
    }

    [Fact]
    public void WasValueUsed_ValorJaUsado_TipoDiferente_Bloqueia()
    {
        var ctx = new SessionContext();
        ctx.Record("field1", "Nicolas Portie Suss", SemanticFieldType.FullName);

        var used = ctx.WasValueUsed("Nicolas Portie Suss", SemanticFieldType.Cpf);
        Assert.True(used);
    }

    [Fact]
    public void WasValueUsed_CpfJaUsado_NaoReutilizaParaEmail()
    {
        var ctx = new SessionContext();
        ctx.Record("cpf", "123.456.789-09", SemanticFieldType.Cpf);

        var used = ctx.WasValueUsed("123.456.789-09", SemanticFieldType.Email);
        Assert.True(used);
    }

    [Fact]
    public void WasValueUsed_TelefoneJaUsado_NaoReutilizaParaCpf()
    {
        var ctx = new SessionContext();
        ctx.Record("phone", "11999998888", SemanticFieldType.Phone);

        var used = ctx.WasValueUsed("11999998888", SemanticFieldType.Cpf);
        Assert.True(used);
    }

    [Fact]
    public void WasValueUsed_NomeJaUsado_PodeReutilizarParaFirstName()
    {
        var ctx = new SessionContext();
        ctx.Record("fullname", "Nicolas Portie Suss", SemanticFieldType.FullName);

        var used = ctx.WasValueUsed("Nicolas Portie Suss", SemanticFieldType.FirstName);
        Assert.False(used);
    }

    [Fact]
    public void Record_ArmazenaCorretamente()
    {
        var ctx = new SessionContext();
        ctx.Record("field1", "valor1", SemanticFieldType.Email);

        Assert.True(ctx.HasField("field1"));
        Assert.Equal("valor1", ctx.GetAssignedValue("field1"));
    }

    [Fact]
    public void AllAssignments_ContemRegistros()
    {
        var ctx = new SessionContext();
        ctx.Record("a", "1", SemanticFieldType.Email);
        ctx.Record("b", "2", SemanticFieldType.Phone);

        Assert.Equal(2, ctx.AllAssignments.Count);
    }

    [Fact]
    public void RetryCount_Incrementa()
    {
        var ctx = new SessionContext();
        ctx.RetryCount = 2;
        Assert.Equal(2, ctx.RetryCount);
    }

    [Fact]
    public void CurrentStep_TotalSteps_Atualiza()
    {
        var ctx = new SessionContext();
        ctx.CurrentStep = 3;
        ctx.TotalSteps = 5;
        Assert.Equal(3, ctx.CurrentStep);
        Assert.Equal(5, ctx.TotalSteps);
    }
}
