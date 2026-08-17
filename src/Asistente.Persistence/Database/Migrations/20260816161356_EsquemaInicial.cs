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
                name: "MAOSOL");

            migrationBuilder.CreateTable(
                name: "ASIS_JORNADA",
                schema: "MAOSOL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USUARIO_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    FECHA_LOCAL = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    INICIO_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIN_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TICKET_PRINCIPAL_ID = table.Column<string>(type: "NVARCHAR2(24)", maxLength: 24, nullable: true),
                    TICKET_PRINCIPAL_CLIENTE_ID = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: true),
                    TICKET_PRINCIPAL_CLIENTE = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: true),
                    TICKET_PRINCIPAL_TITULO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIS_JORNADA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ASIS_USUARIO",
                schema: "MAOSOL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EXTERNAL_USER_ID = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    USUARIO = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    DISPLAY_NAME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    ACTIVO = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    ULTIMO_INGRESO_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIS_USUARIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ASIS_AUDITORIA",
                schema: "MAOSOL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    JORNADA_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ACCION = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    OCURRIDO_EN_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    USUARIO_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DETALLE = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIS_AUDITORIA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ASIS_AUDITORIA_ASIS_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "MAOSOL",
                        principalTable: "ASIS_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASIS_EVENTO",
                schema: "MAOSOL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    JORNADA_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TICKET_ID = table.Column<string>(type: "NVARCHAR2(24)", maxLength: 24, nullable: false),
                    OCURRIDO_EN_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CORRELATION_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CREADO_EN_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIS_EVENTO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ASIS_EVENTO_ASIS_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "MAOSOL",
                        principalTable: "ASIS_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ASIS_SESION",
                schema: "MAOSOL",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    JORNADA_ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    TICKET_ID = table.Column<string>(type: "NVARCHAR2(24)", maxLength: 24, nullable: false),
                    CLIENTE_ID = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    CLIENTE_NOMBRE = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    TITULO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIO_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIN_UTC = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ACCION_ORIGEN = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EDITADA = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIS_SESION", x => x.ID);
                    table.CheckConstraint("CK_ASIS_SESION_FIN", "FIN_UTC IS NULL OR FIN_UTC >= INICIO_UTC");
                    table.ForeignKey(
                        name: "FK_ASIS_SESION_ASIS_JORNADA_JORNADA_ID",
                        column: x => x.JORNADA_ID,
                        principalSchema: "MAOSOL",
                        principalTable: "ASIS_JORNADA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ASIS_AUDITORIA_JORNADA_ID",
                schema: "MAOSOL",
                table: "ASIS_AUDITORIA",
                column: "JORNADA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ASIS_EVENTO_CORR",
                schema: "MAOSOL",
                table: "ASIS_EVENTO",
                column: "CORRELATION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ASIS_EVENTO_JOR_OCU",
                schema: "MAOSOL",
                table: "ASIS_EVENTO",
                columns: new[] { "JORNADA_ID", "OCURRIDO_EN_UTC" });

            migrationBuilder.CreateIndex(
                name: "IX_ASIS_JORNADA_USR_FECHA",
                schema: "MAOSOL",
                table: "ASIS_JORNADA",
                columns: new[] { "USUARIO_ID", "FECHA_LOCAL" });

            migrationBuilder.CreateIndex(
                name: "IX_ASIS_SESION_JOR_INI",
                schema: "MAOSOL",
                table: "ASIS_SESION",
                columns: new[] { "JORNADA_ID", "INICIO_UTC" });

            migrationBuilder.CreateIndex(
                name: "UX_ASIS_USUARIO_EXT",
                schema: "MAOSOL",
                table: "ASIS_USUARIO",
                column: "EXTERNAL_USER_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ASIS_USUARIO_LOGIN",
                schema: "MAOSOL",
                table: "ASIS_USUARIO",
                column: "USUARIO",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ASIS_AUDITORIA",
                schema: "MAOSOL");

            migrationBuilder.DropTable(
                name: "ASIS_EVENTO",
                schema: "MAOSOL");

            migrationBuilder.DropTable(
                name: "ASIS_SESION",
                schema: "MAOSOL");

            migrationBuilder.DropTable(
                name: "ASIS_USUARIO",
                schema: "MAOSOL");

            migrationBuilder.DropTable(
                name: "ASIS_JORNADA",
                schema: "MAOSOL");
        }
    }
}
