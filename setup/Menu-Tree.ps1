$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$CertificateMenuTree = @{
    Title = 'Certificate setup'
    Items = @(
        @{ Label='Setup new certificate'; Key='setup-new'; Type='action' },
        @{ Label='Manage existing certificates'; Key='manage-certs'; Type='action' },
        @{ Label='Backup / Restore'; Key='backup'; Type='submenu'; Items=@(@{ Label='Create backup'; Key='backup-create'; Type='action' },@{ Label='Restore from backup'; Key='backup-restore'; Type='action' },@{ Label='Verify backup'; Key='backup-verify'; Type='action' },@{Key='back';Label='.. Back';Type='back'})},
        @{ Label='Advanced settings'; Key='advanced'; Type='submenu'; Items=@(
            @{ Label='ACME settings'; Key='acme'; Type='action' },
            @{ Label='View logs / diagnostics'; Key='logs-diagnostics'; Type='action' },
            @{ Label='Deployment policies'; Key='policies'; Type='action' },
            @{ Label='View existing policies'; Key='policies-view'; Type='action' },
            @{ Label='Register/Repair orchestrator task'; Key='task-register'; Type='action' },
            @{ Key='back'; Label='.. Back'; Type='back' }
        )},
        @{ Label='Exit'; Key='exit'; Type='action' }
    )
}
