using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace CQEING.AreaUtil.Revit;

/// <summary>La marca invisible de cada línea que crea el add-in, para volver a generar sin duplicar (decisión 12).</summary>
internal static class MarcaAreaUtil
{
    private static readonly Guid IdEsquema = new("83b87101-0818-4eab-985d-f1f3bf45aca0");
    private const string NombreEsquema = "CQEINGAreaUtilLote";
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
        constructor.AddSimpleField(CampoPieza, typeof(string)).SetDocumentation("Estilo de la línea: lote, retiro-… o area-util.");
        return constructor.Finish();
    }

    public static void Marcar(Element e, string pieza)
    {
        var entidad = new Entity(Esquema());
        entidad.Set(CampoPieza, pieza);
        e.SetEntity(entidad);
    }

    private static bool EsPropia(Element e) => Schema.Lookup(IdEsquema) is { } esquema && e.GetEntity(esquema).IsValid();

    /// <summary>Borra todas las líneas del add-in en el modelo; nunca las que no tienen la marca. Devuelve cuántas borró.</summary>
    public static int BorrarTodas(Document doc)
    {
        if (Schema.Lookup(IdEsquema) is null)
            return 0;
        List<ElementId> propias = new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).Where(EsPropia).Select(e => e.Id).ToList();
        if (propias.Count > 0)
            doc.Delete(propias);
        return propias.Count;
    }
}
