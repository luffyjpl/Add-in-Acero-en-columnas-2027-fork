namespace CQEING.Despiece.Calculo.Tests;

public class PrimerPaqueteTests
{
    // Col B-3 medida con el MCP (09-nov): grapas X a 0.064 (debajo del estribo), estribo a 0.074, grapas Y a 0.102; el
    // paquete siguiente, 6 cm más arriba.
    [Fact]
    public void TomaLaPrimeraDeCadaHuellaAunqueLasGrapasVayanDebajoDelEstribo()
    {
        (double desde, double hasta) = PrimerPaquete.Calcular([
            ("x1", 0.064), ("x2", 0.064), ("estribo", 0.074), ("y1", 0.102), ("y2", 0.102),
            ("x1", 0.124), ("x2", 0.124), ("estribo", 0.134), ("y1", 0.162), ("y2", 0.162)]);

        Assert.Equal(0.064, desde, 6);
        Assert.Equal(0.102, hasta, 6);
    }

    [Fact]
    public void LoQueSoloApareceMasArribaNoEsDelPrimerPaquete() =>
        Assert.Equal(0.08, PrimerPaquete.Calcular([("estribo", 0.06), ("x", 0.08), ("caja", 0.30)]).Hasta, 6);

    // Grapas que van cada dos estribos: la que aparece recién en el segundo paquete no es del primero, aunque la
    // separación sea chica.
    [Fact]
    public void NoTomaLoQueApareceDespuesDeQueEmpiezaElSiguientePaquete() =>
        Assert.Equal(0.074, PrimerPaquete.Calcular([
            ("estribo", 0.064), ("x", 0.074), ("estribo", 0.114), ("y", 0.124), ("x", 0.174)]).Hasta, 6);

    [Fact]
    public void ConSeparacionGrandeTambienTomaTodoElPaquete() =>
        Assert.Equal(0.11, PrimerPaquete.Calcular([
            ("x", 0.05), ("estribo", 0.07), ("y", 0.11), ("x", 0.25), ("estribo", 0.27), ("y", 0.31)]).Hasta, 6);

    [Fact]
    public void SoloElEstriboEsUnPaqueteDeUno() =>
        Assert.Equal((0.06, 0.06), PrimerPaquete.Calcular([("estribo", 0.06), ("estribo", 0.16)]));
}
