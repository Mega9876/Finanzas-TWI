using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanzasPersonales.Api.Data.Configurations;

internal static class RelacionesDev3
{
    public static void ConEntidadExistente<T>(
        EntityTypeBuilder<T> builder, string nombreEntidad, string claveForanea,
        string? navegacion = null)
        where T : class
    {
        var principal = builder.Metadata.Model.GetEntityTypes()
            .SingleOrDefault(entity => entity.ClrType.Name == nombreEntidad);

        if (principal is null || principal.HasSharedClrType)
        {
            throw new InvalidOperationException(
                $"Dev3: falta registrar la entidad real {nombreEntidad} en FinanzasDbContext " +
                "antes de aplicar las configuraciones de planificación. " +
                "Integre las entidades y migraciones base del líder; no genere una migración parcial.");
        }

        // Usar dos argumentos: HasOne(string) en el builder genérico interpreta
        // el argumento como navegación CLR, no como nombre de entidad principal.
        builder.HasOne(principal.Name, navegacion)
            .WithMany()
            .HasForeignKey(claveForanea)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
