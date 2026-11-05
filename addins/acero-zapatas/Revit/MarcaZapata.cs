using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// La marca invisible de cada conjunto que crea el add-in (#254), para volver a generar sin duplicar y sin tocar lo que se
/// modeló a mano. Mismo mecanismo que <c>MarcaAcero</c> de vigas, con su propio esquema.
/// </summary>
internal static class MarcaZapata
{
    private static readonly Guid IdEsquema = new("b7a1e3f4-52c8-4d96-9e07-3a64c1f8d025");
    private const string NombreEsquema = "CQEINGZapatasAcero";
    private const string CampoPieza = "Pieza";

    /// <summary>Debe llamarse dentro de una transacción abierta la primera vez (crea el esquema).</summary>
    private static Schema Esquema()
    {
        if (Schema.Lookup(IdEsquema) is { } existente)
            return existente;
        var constructor = new SchemaBuilder(IdEsquema);
        constructor.SetSchemaName(NombreEsquema);
        constructor.SetReadAccessLevel(AccessLevel.Public);
        constructor.SetWriteAccessLevel(AccessLevel.Vendor);
        constructor.SetVendorId("CQEING");
        constructor.AddSimpleField(CampoPieza, typeof(string)).SetDocumentation("Parrilla, dirección y capa de este conjunto.");
        return constructor.Finish();
    }

    public static void Marcar(Element e, string pieza)
    {
        var entidad = new Entity(Esquema());
        entidad.Set(CampoPieza, pieza);
        e.SetEntity(entidad);
    }

    public static bool EsPropia(Element e) =>
        Schema.Lookup(IdEsquema) is { } esquema && e.GetEntity(esquema).IsValid();

    /// <summary>Por zapata, cuántas barras tiene alojadas y cuántas son del add-in.</summary>
    public static (int Total, int Propias) Contar(Element zapata)
    {
        if (RebarHostData.GetRebarHostData(zapata) is not { } datos)
            return (0, 0);
        List<Rebar> barras = datos.GetRebarsInHost().ToList();
        return (barras.Count, barras.Count(EsPropia));
    }

    /// <summary>Borra el acero marcado de la zapata. Nunca toca el que no tiene la marca. Devuelve cuántos conjuntos borró.</summary>
    public static int Borrar(Document doc, Element zapata)
    {
        if (RebarHostData.GetRebarHostData(zapata) is not { } datos)
            return 0;
        List<ElementId> propias = datos.GetRebarsInHost().Where(EsPropia).Select(r => r.Id).ToList();
        if (propias.Count > 0)
            doc.Delete(propias);
        return propias.Count;
    }
}
