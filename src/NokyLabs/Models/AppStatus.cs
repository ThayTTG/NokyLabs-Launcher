namespace NokyLabs.Models;

public enum AppStatus
{
    /// <summary>Existe para este sistema, mas não está na pasta de origem.</summary>
    NotInstalled,

    /// <summary>Instalado e na versão mais recente.</summary>
    Installed,

    /// <summary>Instalado, mas o catálogo tem uma versão mais nova.</summary>
    UpdateAvailable,

    /// <summary>Ainda não existe versão para o sistema atual.</summary>
    Unsupported,

    /// <summary>Card reservado para ideias futuras.</summary>
    ComingSoon,

    /// <summary>Roda no navegador: não precisa instalar.</summary>
    Web,

    /// <summary>Uso restrito: em vez de baixar, a pessoa pede acesso.</summary>
    Restricted
}
