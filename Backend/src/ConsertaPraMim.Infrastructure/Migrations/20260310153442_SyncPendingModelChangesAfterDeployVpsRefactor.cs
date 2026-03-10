using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChangesAfterDeployVpsRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op migration:
            // sincroniza o ModelSnapshot com o modelo atual para remover
            // PendingModelChangesWarning em runtime sem alterar schema existente.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op para manter rollback simetrico desta migracao de snapshot.
        }
    }
}
