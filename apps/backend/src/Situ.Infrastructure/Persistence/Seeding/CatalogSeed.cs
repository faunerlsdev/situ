using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Legal;
using Situ.Domain.Monetization;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Infrastructure.Persistence.Seeding;

internal static class CatalogSeed
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid ColombiaId = SeedId.For("country:CO");

    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedCountries(modelBuilder);
        SeedRegionsAndCities(modelBuilder);
        SeedLegalDocuments(modelBuilder);
        SeedCategories(modelBuilder);
        SeedPlans(modelBuilder);
        SeedSettings(modelBuilder);
        SeedAvatars(modelBuilder);
        SeedStaff(modelBuilder);
    }

    private static void SeedCountries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>().HasData(new
        {
            Id = ColombiaId,
            IsoCode = "CO",
            Name = "Colombia",
            RegionLabel = "Departamento",
            DefaultLocale = "es-CO",
            DefaultCurrency = "COP",
            DefaultTimeZone = "America/Bogota",
            DistanceUnit = DistanceUnit.Kilometers,
            MinimumAge = 14,
            IsEnabled = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });
    }

    private static void SeedRegionsAndCities(ModelBuilder modelBuilder)
    {
        var regions = Departments.Select(department => new
        {
            Id = SeedId.For($"region:{department.Code}"),
            CountryId = ColombiaId,
            Name = department.Name,
            Code = department.Code,
            IsEnabled = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<Region>().HasData(regions.Cast<object>().ToArray());

        var cities = Departments.Select(department => CityRow(
                department.Code,
                department.Capital,
                department.Latitude,
                department.Longitude))
            .Concat(ExtraCities.Select(city => CityRow(city.RegionCode, city.Name, city.Latitude, city.Longitude)));

        modelBuilder.Entity<City>().HasData(cities.Cast<object>().ToArray());
    }

    private static object CityRow(string regionCode, string name, double latitude, double longitude) => new
    {
        Id = SeedId.For($"city:{regionCode}:{name}"),
        RegionId = SeedId.For($"region:{regionCode}"),
        CountryId = ColombiaId,
        Name = name,
        Center = new GeoPoint(latitude, longitude),
        TimeZone = "America/Bogota",
        IsEnabled = true,
        CreatedAt = SeededAt,
        UpdatedAt = (DateTimeOffset?)null,
    };

    private static void SeedLegalDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LegalDocument>().HasData(
            LegalDocumentRow(LegalDocumentType.TermsOfUse, "terminos", "https://situ.local/legal/terminos"),
            LegalDocumentRow(LegalDocumentType.PrivacyPolicy, "privacidad", "https://situ.local/legal/privacidad"));
    }

    private static object LegalDocumentRow(LegalDocumentType type, string slug, string url) => new
    {
        Id = SeedId.For($"legal:{slug}"),
        Type = type,
        CountryId = (Guid?)ColombiaId,
        Locale = "es-CO",
        Version = "2026-01",
        Url = url,
        IsActive = true,
        PublishedAt = SeededAt,
        CreatedAt = SeededAt,
        UpdatedAt = (DateTimeOffset?)null,
    };

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        var rows = Categories.Select(category => new
        {
            Id = SeedId.For($"category:{category.Code}"),
            category.Code,
            category.Name,
            category.Description,
            ButtonIconUrl = $"/assets/categories/{category.Code}.png",
            MapIconUrl = $"/assets/categories/{category.Code}.png",
            category.ColorHex,
            category.SortOrder,
            IsActive = true,
            category.TtlMinutes,
            category.ImportanceWeight,
            category.MediaRequirement,
            ParentCategoryId = (Guid?)null,
            IsAdultOnly = false,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<ReportCategory>().HasData(rows.Cast<object>().ToArray());

        var translations = Categories.Select(category => new
        {
            Id = SeedId.For($"category-translation:{category.Code}:es-CO"),
            CategoryId = SeedId.For($"category:{category.Code}"),
            Locale = "es-CO",
            category.Name,
            category.Description,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<ReportCategoryTranslation>().HasData(translations.Cast<object>().ToArray());
    }

    private static void SeedPlans(ModelBuilder modelBuilder)
    {
        var rows = Plans.Select(plan => new
        {
            Id = SeedId.For($"plan:{plan.Code}"),
            plan.Code,
            plan.Name,
            plan.Description,
            plan.Tier,
            IsActive = true,
            plan.SortOrder,
            plan.ShowsAds,
            plan.MaxDevices,
            plan.MaxLiveMinutesPerStream,
            plan.MaxReportsPerDay,
            plan.MaxAnonymousPostsPerDay,
            plan.MaxSavedRecordings,
            plan.CanSponsorPins,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<SubscriptionPlan>().HasData(rows.Cast<object>().ToArray());
    }

    private static void SeedSettings(ModelBuilder modelBuilder)
    {
        var rows = SystemSettingKeys.Defaults.Select(setting => new
        {
            Id = SeedId.For($"setting:{setting.Key}"),
            Key = setting.Key,
            Value = setting.Value,
            Description = SettingDescriptions[setting.Key],
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<SystemSetting>().HasData(rows.Cast<object>().ToArray());
    }

    private static void SeedStaff(ModelBuilder modelBuilder)
    {
        var roles = StaffRoles.Select(role => new
        {
            Id = SeedId.For($"role:{role.Key}"),
            role.Name,
            role.Description,
            IsSystem = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });

        modelBuilder.Entity<StaffRole>().HasData(roles.Cast<object>().ToArray());

        var permissions = StaffRoles.SelectMany(role => role.Permissions.Select(code => new
        {
            Id = SeedId.For($"permission:{role.Key}:{code}"),
            RoleId = SeedId.For($"role:{role.Key}"),
            Code = code,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        }));

        modelBuilder.Entity<StaffPermission>().HasData(permissions.Cast<object>().ToArray());
    }

    private static readonly Dictionary<string, string> SettingDescriptions = new()
    {
        [SystemSettingKeys.LiveRecordingRetentionDays] = "Días que se conserva la grabación pública de un vivo.",
        [SystemSettingKeys.LiveInternalCopyRetentionDays] = "Días extra que se conserva la copia interna de un vivo.",
        [SystemSettingKeys.ReportRetentionDays] = "Días que se conserva un reporte.",
        [SystemSettingKeys.AnonymousDisplayName] = "Nombre visible de quien publica en anónimo.",
        [SystemSettingKeys.DefaultNotificationRadiusMeters] = "Radio por defecto de las alertas cercanas, en metros.",
        [SystemSettingKeys.MaxDevicesFree] = "Dispositivos activos permitidos en el plan Free.",
        [SystemSettingKeys.LoginMaxFailedAttempts] = "Intentos fallidos antes de bloquear el inicio de sesión.",
        [SystemSettingKeys.LoginLockoutMinutes] = "Minutos que dura el bloqueo por intentos fallidos.",
        [SystemSettingKeys.GuardianMaxSendsPerMinorPerDay] = "Correos de autorización que un menor puede enviar en 24 horas.",
        [SystemSettingKeys.GuardianMinMinutesBetweenSends] = "Minutos mínimos entre correos de autorización de un mismo menor.",
        [SystemSettingKeys.GuardianMaxEmailChangesPerDay] = "Cambios del correo del representante que un menor puede hacer en 24 horas.",
        [SystemSettingKeys.GuardianMaxSendsPerEmailPerHour] = "Solicitudes que un mismo correo de representante puede recibir en una hora.",
        [SystemSettingKeys.GuardianMaxSendsPerEmailPerDay] = "Solicitudes que un mismo correo de representante puede recibir en un día.",
        [SystemSettingKeys.GuardianMaxSendsPerIpPerHour] = "Solicitudes de autorización que una misma IP puede iniciar en una hora.",
        [SystemSettingKeys.GuardianMaxCodeAttempts] = "Intentos fallidos antes de invalidar el código del representante.",
        [SystemSettingKeys.GuardianMaxCodeResendsPer15Minutes] = "Reenvíos del código del representante cada 15 minutos.",
        [SystemSettingKeys.ReportCatalogVersion] = "Versión del catálogo de categorías. Sube en cada cambio del panel.",
        [SystemSettingKeys.ReportMaxImageBytes] = "Tamaño máximo de una imagen de reporte, en bytes.",
        [SystemSettingKeys.ReportMaxVideoBytes] = "Tamaño máximo de un video de reporte, en bytes.",
        [SystemSettingKeys.ReportMaxVideoSeconds] = "Duración máxima de un video de reporte, en segundos.",
        [SystemSettingKeys.ReportMaxImages] = "Imágenes máximas por reporte.",
        [SystemSettingKeys.ReportMaxVideos] = "Videos máximos por reporte.",
        [SystemSettingKeys.ReportConfirmationRadiusMeters] = "Distancia máxima, en metros, para confirmar un reporte.",
        [SystemSettingKeys.ReportConfirmationExtensionPercent] = "Porcentaje del TTL que suma un voto de sigue ahí.",
        [SystemSettingKeys.ReportConfirmationMinExtensionMinutes] = "Minutos mínimos que suma un voto de sigue ahí.",
        [SystemSettingKeys.ReportPendingMediaHours] = "Horas antes de borrar un archivo que no se asoció a un reporte.",
        [SystemSettingKeys.ReportDescriptionMaxLength] = "Máximo de caracteres de la descripción de un reporte. El dominio sigue rechazando más de 1000.",
        [SystemSettingKeys.PanelBrowseCopy] = "Texto del panel inferior cuando se quiere mirar el mapa.",
        [SystemSettingKeys.PanelReportCopy] = "Texto del panel inferior cuando se quiere publicar.",
        [SystemSettingKeys.AppIosMinSupportedVersion] = "Versión mínima de la app iOS.",
        [SystemSettingKeys.AppIosLatestVersion] = "Última versión publicada de la app iOS.",
        [SystemSettingKeys.AppAndroidMinSupportedVersion] = "Versión mínima de la app Android.",
        [SystemSettingKeys.AppAndroidLatestVersion] = "Última versión publicada de la app Android.",
        [SystemSettingKeys.AppWebMinSupportedVersion] = "Versión mínima de la web.",
        [SystemSettingKeys.AppWebLatestVersion] = "Última versión publicada de la web.",
        [SystemSettingKeys.ComplaintAutoReviewThreshold] = "Denuncias distintas que mandan un reporte o comentario a revisión.",
        [SystemSettingKeys.AnonymousCommentsPerDay] = "Comentarios anónimos que una persona puede publicar por día.",
        [SystemSettingKeys.AccountDeletionGraceDays] = "Días de gracia antes de borrar una cuenta.",
        [SystemSettingKeys.NotificationMaxPushesPerHour] = "Pushes que una persona puede recibir en una hora antes de agruparlos.",
        [SystemSettingKeys.NotificationMaxInterestCities] = "Ciudades de interés adicionales permitidas. Hoy aplica a todos los planes.",
        [SystemSettingKeys.NotificationAlertLocationMinutes] = "Minutos que vive la ubicación aproximada usada solo para alertas de proximidad.",
        [SystemSettingKeys.NotificationDispatchBatchSize] = "Trabajos de notificación que se procesan en cada lote.",
        [SystemSettingKeys.NotificationPushBatchSize] = "Mensajes máximos por lote hacia Expo Push.",
        [SystemSettingKeys.ProfileUserNameChangeDays] = "Días mínimos entre cambios del nombre de usuario.",
        [SystemSettingKeys.ProfileReservedUserNames] = "Nombres de usuario reservados, separados por coma. Se tratan igual que un nombre ya tomado.",
        [SystemSettingKeys.ProfileBlockedTerms] = "Términos que no pueden aparecer en el nombre, el usuario o la bio.",
        [SystemSettingKeys.ProfileAvatarMaxBytes] = "Tamaño máximo de una foto de perfil, en bytes.",
        [SystemSettingKeys.LegalTermsUrl] = "URL de los términos de uso.",
        [SystemSettingKeys.LegalPrivacyUrl] = "URL de la política de tratamiento de datos.",
        [SystemSettingKeys.LegalSupportUrl] = "URL de soporte.",
        [SystemSettingKeys.PanelCopyByCountry] = "Textos del panel inferior por país e idioma, en JSON.",
        [SystemSettingKeys.MapCellDegrees] = "Lado de una celda del mapa en tiempo real, en grados.",
        [SystemSettingKeys.MapMaxCells] = "Celdas máximas que una conexión puede seguir a la vez.",
        [SystemSettingKeys.MapSubscribeCallsPerMinute] = "Cambios de suscripción permitidos por conexión en un minuto.",
        [SystemSettingKeys.MapCoalesceIntervalMilliseconds] = "Milisegundos entre tandas de avisos del mapa en tiempo real.",
        [SystemSettingKeys.PhoneRequiredForMinors] = "Si es true, un menor debe dejar teléfono al registrarse. Pregunta abierta de minimización (RF-MIN-06).",
        [SystemSettingKeys.SocialTabsEnabled] = "Muestra las pestañas Mensajes y Chisme. Apagado las oculta sin borrar las pantallas.",
        [SystemSettingKeys.WebSessionRefreshDays] = "Días de la sesión web. Cada renovación vuelve a contar este plazo.",
        [SystemSettingKeys.WebSessionRememberDays] = "Días de la sesión web cuando la persona marca recordarme en este dispositivo.",
    };

    private static void SeedAvatars(ModelBuilder modelBuilder)
    {
        var names = new[]
        {
            "Isotipo",
            "Pin",
            "Megáfono",
            "Mapa",
            "En vivo",
            "Mensajes",
            "Noticias",
            "Buscar",
            "Isotipo oscuro",
            "Genérico",
            "Evento",
            "Vía",
        };
        var rows = names.Select((name, index) => new
        {
            Id = SeedId.For($"avatar:{(index + 1):00}"),
            Name = name,
            ImageUrl = $"/assets/avatars/{(index + 1):00}.png",
            SortOrder = index + 1,
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = (DateTimeOffset?)null,
        });
        modelBuilder.Entity<AvatarGalleryItem>().HasData(rows.Cast<object>().ToArray());
    }

    private static readonly (string Code, string Name, string Description, string ColorHex, int SortOrder, int TtlMinutes, double ImportanceWeight, MediaRequirement MediaRequirement)[] Categories =
    [
        ("accidente_transito", "Accidente de tránsito", "Choques, atropellos y vehículos varados.", "#E53935", 1, 90, 8, MediaRequirement.None),
        ("hurto", "Hurto", "Hurtos que están ocurriendo ahora.", "#8E24AA", 2, 240, 7, MediaRequirement.None),
        ("puesto_control", "Puesto de control", "Retenes, controles y puestos de vigilancia.", "#1E88E5", 3, 120, 4, MediaRequirement.None),
        ("basuras", "Basuras", "Basura acumulada, escombros o vertimientos.", "#43A047", 4, 1440, 2, MediaRequirement.None),
        ("via_cerrada", "Vía cerrada", "Cierres y tramos que no se pueden pasar.", "#FB8C00", 5, 240, 6, MediaRequirement.None),
        ("derrumbe", "Derrumbe", "Derrumbes y material sobre la vía.", "#6D4C41", 6, 480, 8, MediaRequirement.None),
        ("inundacion", "Inundación", "Calles anegadas, crecientes y desbordamientos.", "#039BE5", 7, 360, 8, MediaRequirement.None),
        ("manifestacion", "Manifestación", "Marchas, plantones y bloqueos.", "#8D6E63", 8, 180, 6, MediaRequirement.None),
        ("incendio", "Incendio", "Incendios, humo o explosiones.", "#F4511E", 9, 180, 9, MediaRequirement.None),
        ("obra_vial", "Obra vial", "Obras que cambian el paso por la vía.", "#FDD835", 10, 720, 3, MediaRequirement.None),
        ("corte_servicio", "Corte de servicio", "Cortes de agua, luz o gas.", "#546E7A", 11, 360, 4, MediaRequirement.None),
        ("evento", "Evento", "Eventos que ocupan el espacio público.", "#7CB342", 12, 240, 2, MediaRequirement.None),
    ];

    private static readonly (string Code, string Name, string Description, PlanTier Tier, int SortOrder, bool ShowsAds, int? MaxDevices, int? MaxLiveMinutesPerStream, int? MaxReportsPerDay, int? MaxAnonymousPostsPerDay, int? MaxSavedRecordings, bool CanSponsorPins)[] Plans =
    [
        ("free", "Free", "Mapa, reportes y vivos con publicidad. Hasta 2 dispositivos.", PlanTier.Free, 0, true, 2, 15, 10, 3, 0, false),
        ("basic", "Basic", "Sin publicidad, con más reportes y vivos más largos.", PlanTier.Basic, 1, false, 3, 60, 30, 15, 5, false),
        ("pro", "Pro", "Reportes y anónimos sin tope, y pines patrocinados.", PlanTier.Pro, 2, false, 5, 180, null, null, 30, true),
        ("advanced", "Advanced", "Sin topes de uso y con pines patrocinados.", PlanTier.Advanced, 3, false, null, null, null, null, null, true),
    ];

    private static readonly (string Key, string Name, string Description, string[] Permissions)[] StaffRoles =
    [
        ("super-admin", "Super admin", "Acceso total al panel, incluida la gestión del equipo.",
            StaffPermissionCodes.All),
        ("moderator", "Moderador", "Resuelve denuncias, suspende cuentas, corta vivos y atiende retiros.",
        [
            StaffPermissionCodes.ComplaintsResolve,
            StaffPermissionCodes.UsersView,
            StaffPermissionCodes.UsersSuspend,
            StaffPermissionCodes.LiveCut,
            StaffPermissionCodes.TakedownsHandle,
        ]),
        ("support", "Soporte", "Consulta cuentas.",
        [
            StaffPermissionCodes.UsersView,
        ]),
        ("content-manager", "Gestor de contenido", "Administra categorías y consulta los planes.",
        [
            StaffPermissionCodes.CategoriesManage,
            StaffPermissionCodes.PlansView,
        ]),
        ("auditor", "Auditor", "Consulta la bitácora.",
        [
            StaffPermissionCodes.AuditView,
        ]),
        ("finance", "Finanzas", "Administra planes y consulta la auditoría.",
        [
            StaffPermissionCodes.PlansManage,
            StaffPermissionCodes.AuditView,
        ]),
    ];

    private static readonly (string Code, string Name, string Capital, double Latitude, double Longitude)[] Departments =
    [
        ("91", "Amazonas", "Leticia", -4.2153, -69.9406),
        ("05", "Antioquia", "Medellín", 6.2442, -75.5812),
        ("81", "Arauca", "Arauca", 7.0903, -70.7617),
        ("08", "Atlántico", "Barranquilla", 10.9685, -74.7813),
        ("11", "Bogotá, D.C.", "Bogotá", 4.7110, -74.0721),
        ("13", "Bolívar", "Cartagena de Indias", 10.3910, -75.4794),
        ("15", "Boyacá", "Tunja", 5.5353, -73.3678),
        ("17", "Caldas", "Manizales", 5.0689, -75.5174),
        ("18", "Caquetá", "Florencia", 1.6144, -75.6062),
        ("85", "Casanare", "Yopal", 5.3378, -72.3959),
        ("19", "Cauca", "Popayán", 2.4448, -76.6147),
        ("20", "Cesar", "Valledupar", 10.4631, -73.2532),
        ("27", "Chocó", "Quibdó", 5.6947, -76.6611),
        ("23", "Córdoba", "Montería", 8.7479, -75.8814),
        ("25", "Cundinamarca", "Soacha", 4.5794, -74.2168),
        ("94", "Guainía", "Inírida", 3.8653, -67.9239),
        ("95", "Guaviare", "San José del Guaviare", 2.5729, -72.6459),
        ("41", "Huila", "Neiva", 2.9273, -75.2819),
        ("44", "La Guajira", "Riohacha", 11.5444, -72.9072),
        ("47", "Magdalena", "Santa Marta", 11.2408, -74.1990),
        ("50", "Meta", "Villavicencio", 4.1420, -73.6266),
        ("52", "Nariño", "Pasto", 1.2136, -77.2811),
        ("54", "Norte de Santander", "Cúcuta", 7.8939, -72.5078),
        ("86", "Putumayo", "Mocoa", 1.1528, -76.6520),
        ("63", "Quindío", "Armenia", 4.5339, -75.6811),
        ("66", "Risaralda", "Pereira", 4.8087, -75.6906),
        ("88", "San Andrés y Providencia", "San Andrés", 12.5847, -81.7006),
        ("68", "Santander", "Bucaramanga", 7.1193, -73.1227),
        ("70", "Sucre", "Sincelejo", 9.3047, -75.3978),
        ("73", "Tolima", "Ibagué", 4.4389, -75.2322),
        ("76", "Valle del Cauca", "Cali", 3.4516, -76.5320),
        ("97", "Vaupés", "Mitú", 1.1983, -70.1735),
        ("99", "Vichada", "Puerto Carreño", 6.1890, -67.4859),
    ];

    private static readonly (string RegionCode, string Region, string Name, double Latitude, double Longitude)[] ExtraCities =
    [
        ("05", "Antioquia", "Bello", 6.3373, -75.5580),
        ("05", "Antioquia", "Envigado", 6.1699, -75.5830),
        ("05", "Antioquia", "Itagüí", 6.1719, -75.6113),
        ("05", "Antioquia", "Rionegro", 6.1554, -75.3737),
        ("08", "Atlántico", "Soledad", 10.9184, -74.7696),
        ("25", "Cundinamarca", "Chía", 4.8613, -74.0586),
        ("25", "Cundinamarca", "Facatativá", 4.8146, -74.3548),
        ("25", "Cundinamarca", "Fusagasugá", 4.3365, -74.3638),
        ("25", "Cundinamarca", "Girardot", 4.3030, -74.8008),
        ("25", "Cundinamarca", "Zipaquirá", 5.0221, -74.0048),
        ("68", "Santander", "Barrancabermeja", 7.0653, -73.8547),
        ("68", "Santander", "Floridablanca", 7.0622, -73.0862),
        ("76", "Valle del Cauca", "Buenaventura", 3.8801, -77.0312),
        ("76", "Valle del Cauca", "Palmira", 3.5394, -76.3036),
        ("76", "Valle del Cauca", "Tuluá", 4.0847, -76.1954),
    ];
}

internal static class SeedId
{
    public static Guid For(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("situ-seed:" + key));
        return new Guid(hash.AsSpan(0, 16));
    }
}
