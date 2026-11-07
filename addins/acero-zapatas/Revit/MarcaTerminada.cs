using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace CQEING.Zapatas.Revit;

/// <summary>
/// La marca de "terminada" de una zapata, igual que la de los pórticos de vigas (<c>MarcaTerminado</c>): queda puesta al
/// generar el acero, se pone o se quita con la palomita del tablero y la zapata sale en verde en la planta. Se guarda en
/// la zapata, con su propio esquema: un esquema creado no se puede cambiar.
/// </summary>
internal static class MarcaTerminada
{
    private static readonly Guid IdEsquema = new("f634c8e8-eaf8-414d-ad71-aca4626e619b");
    private const string NombreEsquema = "CQEINGZapatasTerminada";
    private const string CampoTerminada = "Terminada";

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
        constructor.AddSimpleField(CampoTerminada, typeof(bool))
            .SetDocumentation("La zapata se marcó como terminada en el tablero de zapatas.");
        return constructor.Finish();
    }

    /// <summary>Dentro de una transacción. Sin marca, borra la que hubiera.</summary>
    public static void Marcar(Element zapata, bool terminada)
    {
        Schema esquema = Esquema();
        if (!terminada)
        {
            zapata.DeleteEntity(esquema);
            return;
        }
        var entidad = new Entity(esquema);
        entidad.Set(CampoTerminada, true);
        zapata.SetEntity(entidad);
    }

    /// <summary>Si <paramref name="zapata"/> tiene la marca.</summary>
    public static bool Tiene(Element? zapata)
    {
        if (zapata is null || Schema.Lookup(IdEsquema) is not { } esquema)
            return false;
        Entity entidad = zapata.GetEntity(esquema);
        return entidad.IsValid() && entidad.Get<bool>(CampoTerminada);
    }
}
