using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jw.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Floors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FloorAddedHoleMarks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LineAS = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineAE = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineBS = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineBE = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    HoleCenter = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    HasBeam = table.Column<bool>(type: "bit", nullable: false),
                    JwBeamDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorAddedHoleMarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorBeams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeamCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FloorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GongQu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasQieGe = table.Column<bool>(type: "bit", nullable: false),
                    QieGeCount = table.Column<int>(type: "int", nullable: false),
                    IsParentBeam = table.Column<bool>(type: "bit", nullable: false),
                    IsQiegeBeam = table.Column<bool>(type: "bit", nullable: false),
                    Length = table.Column<double>(type: "float", nullable: false),
                    XXLength = table.Column<double>(type: "float", nullable: false),
                    HasStartSide = table.Column<bool>(type: "bit", nullable: false),
                    HasEndSide = table.Column<bool>(type: "bit", nullable: false),
                    StartTelosType = table.Column<int>(type: "int", nullable: false),
                    EndTelosType = table.Column<int>(type: "int", nullable: false),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BeamXHId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BeamXHName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartCenter = table.Column<double>(type: "float", nullable: false),
                    EndCenter = table.Column<double>(type: "float", nullable: false),
                    HasBFG = table.Column<bool>(type: "bit", nullable: false),
                    InitialBeamCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaiFangGTBDistance = table.Column<int>(type: "int", nullable: false),
                    BeamSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasCsv = table.Column<bool>(type: "bit", nullable: false),
                    Jiaodu = table.Column<double>(type: "float", nullable: false),
                    CenterPoint = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorBeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorCuttings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FirstPoint = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SecondPoint = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ThirdPoint = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorCuttings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorDownPillars",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LineAS = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineAE = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineBS = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LineBE = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    HasBeam = table.Column<bool>(type: "bit", nullable: false),
                    HasPillar = table.Column<bool>(type: "bit", nullable: false),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorDownPillars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorLianjies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Start = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    End = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Length = table.Column<double>(type: "float", nullable: false),
                    ProjectSubName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreateFrom = table.Column<int>(type: "int", nullable: false),
                    HasStartChange = table.Column<bool>(type: "bit", nullable: false),
                    HasEndChange = table.Column<bool>(type: "bit", nullable: false),
                    StartOriginal = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EndOriginal = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VPLStartPosition = table.Column<int>(type: "int", nullable: false),
                    VPLEndPosition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorLianjies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorLinkParts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BujianName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BeamId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Directed = table.Column<int>(type: "int", nullable: false),
                    GouJianType = table.Column<int>(type: "int", nullable: false),
                    CreateFrom = table.Column<int>(type: "int", nullable: false),
                    IsLianjie = table.Column<bool>(type: "bit", nullable: false),
                    IsNoBeam = table.Column<bool>(type: "bit", nullable: false),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorLinkParts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorPillars",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PillarCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlocksCount = table.Column<int>(type: "int", nullable: false),
                    BaseType = table.Column<int>(type: "int", nullable: false),
                    FirstLocation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FirstWidth = table.Column<double>(type: "float", nullable: true),
                    FirstHeight = table.Column<double>(type: "float", nullable: true),
                    CenterLocation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CenterWidth = table.Column<double>(type: "float", nullable: true),
                    CenterHeight = table.Column<double>(type: "float", nullable: true),
                    LastLocation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastWidth = table.Column<double>(type: "float", nullable: true),
                    LastHeight = table.Column<double>(type: "float", nullable: true),
                    TaggTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JwProjectSubDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPillars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorBeamVerticals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    JwBeamDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Center = table.Column<double>(type: "float", nullable: false),
                    HasPre = table.Column<bool>(type: "bit", nullable: false),
                    HasLast = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorBeamVerticals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorBeamVerticals_FloorBeams_JwBeamDataId",
                        column: x => x.JwBeamDataId,
                        principalTable: "FloorBeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FloorHoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HasTop = table.Column<bool>(type: "bit", nullable: false),
                    HasCenter = table.Column<bool>(type: "bit", nullable: false),
                    HasBottom = table.Column<bool>(type: "bit", nullable: false),
                    KongNum = table.Column<int>(type: "int", nullable: false),
                    FirstCreateFrom = table.Column<int>(type: "int", nullable: false),
                    ChangeFrom = table.Column<int>(type: "int", nullable: false),
                    IsStart = table.Column<bool>(type: "bit", nullable: false),
                    IsEnd = table.Column<bool>(type: "bit", nullable: false),
                    HoleType = table.Column<int>(type: "int", nullable: false),
                    JwBeamDataId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HasPreLinkHole = table.Column<bool>(type: "bit", nullable: false),
                    HasBhLinkHole = table.Column<bool>(type: "bit", nullable: false),
                    IsMachining = table.Column<bool>(type: "bit", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorHoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorHoles_FloorBeams_JwBeamDataId",
                        column: x => x.JwBeamDataId,
                        principalTable: "FloorBeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Floors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeamCount = table.Column<int>(type: "int", nullable: false),
                    HorizontalBeamsCount = table.Column<int>(type: "int", nullable: false),
                    VerticalBeamsCount = table.Column<int>(type: "int", nullable: false),
                    PillarCount = table.Column<int>(type: "int", nullable: false),
                    KPillarCount = table.Column<int>(type: "int", nullable: false),
                    SinglePillarCount = table.Column<int>(type: "int", nullable: false),
                    BCount = table.Column<int>(type: "int", nullable: false),
                    BGCount = table.Column<int>(type: "int", nullable: false),
                    ExportCount = table.Column<int>(type: "int", nullable: false),
                    Scale = table.Column<double>(type: "float", nullable: false),
                    ScaleText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MarkBeam = table.Column<int>(type: "int", nullable: true),
                    LocationX = table.Column<double>(type: "float", nullable: false),
                    LocationY = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    DirectionType = table.Column<int>(type: "int", nullable: false),
                    KPillarType = table.Column<int>(type: "int", nullable: false),
                    BraceComponent = table.Column<int>(type: "int", nullable: false),
                    MinimalStrategy = table.Column<bool>(type: "bit", nullable: false),
                    ParseOptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParseElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Floors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Floors_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Floors_StoredFiles_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FloorAddedHoleMarks_JwBeamDataId",
                table: "FloorAddedHoleMarks",
                column: "JwBeamDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorAddedHoleMarks_JwProjectSubDataId",
                table: "FloorAddedHoleMarks",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorBeams_JwProjectSubDataId",
                table: "FloorBeams",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorBeamVerticals_JwBeamDataId",
                table: "FloorBeamVerticals",
                column: "JwBeamDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorCuttings_JwProjectSubDataId",
                table: "FloorCuttings",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorDownPillars_JwProjectSubDataId",
                table: "FloorDownPillars",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorHoles_JwBeamDataId",
                table: "FloorHoles",
                column: "JwBeamDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorLianjies_JwProjectSubDataId",
                table: "FloorLianjies",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorLinkParts_JwProjectSubDataId",
                table: "FloorLinkParts",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorPillars_JwProjectSubDataId",
                table: "FloorPillars",
                column: "JwProjectSubDataId");

            migrationBuilder.CreateIndex(
                name: "IX_Floors_ProjectId_Name",
                table: "Floors",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Floors_SourceFileId",
                table: "Floors",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Sha256",
                table: "StoredFiles",
                column: "Sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloorAddedHoleMarks");

            migrationBuilder.DropTable(
                name: "FloorBeamVerticals");

            migrationBuilder.DropTable(
                name: "FloorCuttings");

            migrationBuilder.DropTable(
                name: "FloorDownPillars");

            migrationBuilder.DropTable(
                name: "FloorHoles");

            migrationBuilder.DropTable(
                name: "FloorLianjies");

            migrationBuilder.DropTable(
                name: "FloorLinkParts");

            migrationBuilder.DropTable(
                name: "FloorPillars");

            migrationBuilder.DropTable(
                name: "Floors");

            migrationBuilder.DropTable(
                name: "FloorBeams");

            migrationBuilder.DropTable(
                name: "StoredFiles");
        }
    }
}
