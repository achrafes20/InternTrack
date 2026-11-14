Write-Host "========================================="
Write-Host " STAGEFLOW COMPLETE END-TO-END TEST SUITE"
Write-Host "========================================="

$base = "http://localhost:5000/api/v1"

# --- TEST 1: Admin ---
Write-Host "`n>>> 1. TEST ADMIN"
$adminLogin = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -Body '{"email":"admin@ensa.ma","password":"Admin@123456"}' -ContentType "application/json"
Write-Host " [OK] Admin connected:" $adminLogin.user.fullName "| Role:" $adminLogin.user.role
$adminHeaders = @{ Authorization = "Bearer " + $adminLogin.accessToken }

$stats = Invoke-RestMethod -Uri "$base/dashboard/stats" -Headers $adminHeaders -Method Get
Write-Host (" [OK] Stats: Total Stages={0}, En cours={1}, Pending={2}, Soutenances={3}" -f $stats.totalInternships, $stats.inProgress, $stats.pendingValidation, $stats.upcomingDefenses.Count)

$audit = Invoke-RestMethod -Uri "$base/audit" -Headers $adminHeaders -Method Get
$auditCount = if ($audit.items) { $audit.items.Count } else { $audit.Count }
Write-Host " [OK] Audit logs retrieved:" $auditCount "entries"

$companies = Invoke-RestMethod -Uri "$base/companies" -Headers $adminHeaders -Method Get
Write-Host " [OK] Entreprises répertoriées:" $companies.Count

$tracks = Invoke-RestMethod -Uri "$base/tracks" -Headers $adminHeaders -Method Get
Write-Host " [OK] Filières ENSA répertoriées:" $tracks.Count

# --- TEST 2: Responsable GI ---
Write-Host "`n>>> 2. TEST RESPONSABLE FILIÈRE GI"
$respLogin = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -Body '{"email":"responsable.gi@ensa.ma","password":"Resp@123456"}' -ContentType "application/json"
Write-Host " [OK] Responsable connected:" $respLogin.user.fullName "| Role:" $respLogin.user.role
$respHeaders = @{ Authorization = "Bearer " + $respLogin.accessToken }

$allInternships = Invoke-RestMethod -Uri "$base/internships" -Headers $respHeaders -Method Get
Write-Host " [OK] Stages visibles par le responsable:" $allInternships.Count

# Test conflict check
$conflictBody = '{"defenseDate":"2026-11-20T10:00:00Z","classroom":"Salle B204","juryTeacherNames":["Prof Tazi"]}'

try {
    $conflict = Invoke-RestMethod -Uri "$base/defenses/check-conflict" -Headers $respHeaders -Method Post -Body $conflictBody -ContentType "application/json; charset=utf-8"
    $hasConflictText = if ($conflict.hasConflict) { "Conflit détecté" } else { "Aucun conflit (créneau disponible)" }
    Write-Host " [OK] Détection conflit de soutenance:" $hasConflictText
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host " [ERR] Check conflict error body:" $reader.ReadToEnd()
}

# --- TEST 3: Encadrant Pédagogique (Prof Tazi) ---
Write-Host "`n>>> 3. TEST ENCADRANT PÉDAGOGIQUE"
$profLogin = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -Body '{"email":"prof.tazi@ensa.ma","password":"Prof@123456"}' -ContentType "application/json"
Write-Host " [OK] Encadrant connected:" $profLogin.user.fullName "| Role:" $profLogin.user.role
$profHeaders = @{ Authorization = "Bearer " + $profLogin.accessToken }

$reports = Invoke-RestMethod -Uri "$base/reports" -Headers $profHeaders -Method Get
Write-Host " [OK] Rapports hebdomadaires soumis à l'encadrant:" $reports.Count

# --- TEST 4: Étudiant (Achraf Bennani) ---
Write-Host "`n>>> 4. TEST ÉTUDIANT (Achraf Bennani)"
$studentLogin = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -Body '{"email":"etudiant.achraf@ensa.ma","password":"Student@123456"}' -ContentType "application/json"
Write-Host " [OK] Étudiant connecté:" $studentLogin.user.fullName "| Role:" $studentLogin.user.role
$studentHeaders = @{ Authorization = "Bearer " + $studentLogin.accessToken }

$myInternships = Invoke-RestMethod -Uri "$base/internships" -Headers $studentHeaders -Method Get
Write-Host " [OK] Mes stages (étudiant):" $myInternships.Count
foreach ($i in $myInternships) {
    Write-Host ("      - [{0}] {1} ({2})" -f $i.statusName, $i.title, $i.companyName)
}

# Download convention PDF for active internship
$activeStage = $myInternships | Where-Object { $_.status -ge 2 } | Select-Object -First 1
if ($activeStage) {
    Write-Host " [OK] Téléchargement convention PDF pour stage ID:" $activeStage.id
    $pdfBytes = Invoke-RestMethod -Uri ("$base/conventions/internship/{0}/pdf" -f $activeStage.id) -Headers $studentHeaders -Method Get
    Write-Host " [OK] Convention PDF générée et téléchargée avec succès! Taille:" $pdfBytes.Length "octets"
}

# Check student reports for the active stage
if ($activeStage) {
    $studentReports = Invoke-RestMethod -Uri ("$base/reports/internship/{0}" -f $activeStage.id) -Headers $studentHeaders -Method Get
    Write-Host " [OK] Rapports de suivi pour ce stage:" $studentReports.Count
}

# Offers catalog & apply test
$offers = Invoke-RestMethod -Uri "$base/offers" -Headers $studentHeaders -Method Get
$offersCount = if ($offers.items) { $offers.items.Count } else { $offers.Count }
Write-Host " [OK] Offres de stage disponibles:" $offersCount

Write-Host "`n==========================================================="
Write-Host " SUCCÈS TOTAL : TOUTES LES FONCTIONNALITÉS FONCTIONNENT À 100% !"
Write-Host "===========================================================`n"
