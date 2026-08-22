using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asistente.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "T_PERMISO",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CODIGO = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DESCRIPCION = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_PERMISO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "T_USUARIO",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    USUARIO = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NOMBRE_COMPLETO = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CLAVE_HASH = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ACTIVO = table.Column<bool>(type: "bit", nullable: false),
                    FECHA_ALTA_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ULTIMO_INGRESO_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ULTIMO_CAMBIO_CLAVE_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CANTIDAD_INTENTO_FALLIDO = table.Column<int>(type: "int", nullable: false),
                    BLOQUEADO_HASTA_UTC = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_USUARIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "T_JORNADA",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    USUARIO_ID = table.Column<long>(type: "bigint", nullable: false),
                    FECHA_LOCAL = table.Column<DateTime>(type: "datetime2", nullable: false),
                    INICIO_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FIN_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ESTADO = table.Column<int>(type: "int", nullable: false),
                    TICKET_PRINCIPAL_ID = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    TICKET_PRINCIPAL_CLIENTE_ID = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TICKET_PRINCIPAL_CLIENTE_NOMBRE = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TICKET_PRINCIPAL_TITULO = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VERSION = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_JORNADA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_T_JORNADA_T_USUARIO_USUARIO_ID",
                        column: x => x.USUARIO_ID,
                        principalSchema: "dbo",
                        principalTable: "T_USUARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_SESION_USUARIO",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    USUARIO_ID = table.Column<long>(type: "bigint", nullable: false),
                    INICIO_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FIN_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DIRECCION_IP = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    AGENTE = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MOTIVO_CIERRE = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_SESION_USUARIO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_T_SESION_USUARIO_T_USUARIO_USUARIO_ID",
                        column: x => x.USUARIO_ID,
                        principalSchema: "dbo",
                        principalTable: "T_USUARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_USUARIO_PERMISO",
                schema: "dbo",
                columns: table => new
                {
                    USUARIO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PERMISO_ID = table.Column<long>(type: "bigint", nullable: false),
                    OTORGADO_EN_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_USUARIO_PERMISO", x => new { x.USUARIO_ID, x.PERMISO_ID });
                    table.ForeignKey(
                        name: "FK_T_USUARIO_PERMISO_T_PERMISO_PERMISO_ID",
                        column: x => x.PERMISO_ID,
                        principalSchema: "dbo",
                        principalTable: "T_PERMISO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_T_USUARIO_PERMISO_T_USUARIO_USUARIO_ID",
                        column: x => x.USUARIO_ID,
                        principalSchema: "dbo",
                        principalTable: "T_USUARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_AUDITORIA",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JORNADA_ID = table.Column<long>(type: "bigint", nullable: false),
                    ACCION = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OCURRIDO_EN_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    USUARIO_ID = table.Column<long>(type: "bigint", nullable: false),
                    DETALLE = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_AUDITORIA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_T_AUDITORIA_T_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "dbo",
                        principalTable: "T_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_EVENTO",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JORNADA_ID = table.Column<long>(type: "bigint", nullable: false),
                    TIPO = table.Column<int>(type: "int", nullable: false),
                    TICKET_ID = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    OCURRIDO_EN_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CORRELACION_ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CREADO_EN_UTC = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_EVENTO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_T_EVENTO_T_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "dbo",
                        principalTable: "T_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_PLANILLA",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JORNADA_ID = table.Column<long>(type: "bigint", nullable: false),
                    USUARIO_ID = table.Column<long>(type: "bigint", nullable: false),
                    GENERADA_EN_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NOMBRE_ARCHIVO = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    HASH_SHA256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CANTIDAD_FILA = table.Column<int>(type: "int", nullable: false),
                    NUMERO_GENERACION = table.Column<int>(type: "int", nullable: false),
                    CONTENIDO = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_PLANILLA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_T_PLANILLA_T_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "dbo",
                        principalTable: "T_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_SESION",
                schema: "dbo",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JORNADA_ID = table.Column<long>(type: "bigint", nullable: false),
                    TICKET_ID = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CLIENTE_ID = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CLIENTE_NOMBRE = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TITULO = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TIPO = table.Column<int>(type: "int", nullable: false),
                    INICIO_UTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FIN_UTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ACCION_ORIGEN = table.Column<int>(type: "int", nullable: false),
                    EDITADA = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_SESION", x => x.ID);
                    table.CheckConstraint("CK_T_SESION_FIN", "FIN_UTC IS NULL OR FIN_UTC >= INICIO_UTC");
                    table.ForeignKey(
                        name: "FK_T_SESION_T_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "dbo",
                        principalTable: "T_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_T_AUDITORIA_JORNADA",
                schema: "dbo",
                table: "T_AUDITORIA",
                column: "JORNADA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_T_EVENTO_CORRELACION",
                schema: "dbo",
                table: "T_EVENTO",
                column: "CORRELACION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_T_EVENTO_JORNADA_OCURRIDO",
                schema: "dbo",
                table: "T_EVENTO",
                columns: new[] { "JORNADA_ID", "OCURRIDO_EN_UTC" });

            migrationBuilder.CreateIndex(
                name: "IX_T_JORNADA_USUARIO_FECHA",
                schema: "dbo",
                table: "T_JORNADA",
                columns: new[] { "USUARIO_ID", "FECHA_LOCAL" });

            migrationBuilder.CreateIndex(
                name: "UX_T_PERMISO_CODIGO",
                schema: "dbo",
                table: "T_PERMISO",
                column: "CODIGO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_T_PLANILLA_JORNADA_GENERACION",
                schema: "dbo",
                table: "T_PLANILLA",
                columns: new[] { "JORNADA_ID", "NUMERO_GENERACION" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_T_SESION_JORNADA_INICIO",
                schema: "dbo",
                table: "T_SESION",
                columns: new[] { "JORNADA_ID", "INICIO_UTC" });

            migrationBuilder.CreateIndex(
                name: "IX_T_SESION_USUARIO_USUARIO_INICIO",
                schema: "dbo",
                table: "T_SESION_USUARIO",
                columns: new[] { "USUARIO_ID", "INICIO_UTC" });

            migrationBuilder.CreateIndex(
                name: "UX_T_USUARIO_USUARIO",
                schema: "dbo",
                table: "T_USUARIO",
                column: "USUARIO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_T_USUARIO_PERMISO_PERMISO",
                schema: "dbo",
                table: "T_USUARIO_PERMISO",
                column: "PERMISO_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "T_AUDITORIA",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_EVENTO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_PLANILLA",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_SESION",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_SESION_USUARIO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_USUARIO_PERMISO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_JORNADA",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_PERMISO",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "T_USUARIO",
                schema: "dbo");
        }
    }
}
