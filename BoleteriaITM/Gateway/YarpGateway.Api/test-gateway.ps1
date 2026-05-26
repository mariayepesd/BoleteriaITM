# Script para probar el YarpGateway.Api
# Ejecutar desde: C:\Users\MajoY\source\repos\BoleteriaITM\src\Gateway\YarpGateway.Api

Write-Host "🚀 Testing YarpGateway.Api..." -ForegroundColor Cyan

# Ignorar advertencias de certificado autofirmado (solo para desarrollo)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$baseUrl = "https://localhost:5000"
$correlationId = [guid]::NewGuid().ToString()

# 1. Test Health Check
Write-Host "`n📋 1. Testing Health Check..." -ForegroundColor Yellow
try {
	$health = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -SkipCertificateCheck
	Write-Host "✅ Health Check OK: $health" -ForegroundColor Green
}
catch {
	Write-Host "❌ Health Check Failed: $_" -ForegroundColor Red
}

# 2. Generar Token JWT
Write-Host "`n🔑 2. Generating JWT Token..." -ForegroundColor Yellow
try {
	$tokenResponse = Invoke-RestMethod -Uri "$baseUrl/auth/token" -Method Post -SkipCertificateCheck
	$token = $tokenResponse.token
	Write-Host "✅ Token Generated (Expires In: $($tokenResponse.expiresIn)s)" -ForegroundColor Green
	Write-Host "Token: $($token.Substring(0, 50))..." -ForegroundColor Gray
}
catch {
	Write-Host "❌ Token Generation Failed: $_" -ForegroundColor Red
	exit
}

# 3. Test Sin Token (debe fallar)
Write-Host "`n🚫 3. Testing Request WITHOUT Token (should fail)..." -ForegroundColor Yellow
try {
	$response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
		-Headers @{ "X-Correlation-ID" = $correlationId } `
		-Method Get `
		-SkipCertificateCheck
	Write-Host "⚠️  Unexpected Success (security issue)" -ForegroundColor Yellow
}
catch {
	if ($_.Exception.Response.StatusCode -eq 401) {
		Write-Host "✅ Correctly Rejected (401 Unauthorized)" -ForegroundColor Green
	}
	else {
		Write-Host "❌ Unexpected Error: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
	}
}

# 4. Test Con Token (será reenviado a Order.Api en localhost:5001)
Write-Host "`n✔️  4. Testing Request WITH Token..." -ForegroundColor Yellow
try {
	$response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
		-Headers @{ 
			Authorization = "Bearer $token"
			"X-Correlation-ID" = $correlationId
		} `
		-Method Get `
		-SkipCertificateCheck
	Write-Host "✅ Request Forwarded Successfully" -ForegroundColor Green
	Write-Host "Response Headers:" -ForegroundColor Gray
	Write-Host "  X-Correlation-ID: $correlationId" -ForegroundColor Gray
}
catch {
	# Es normal que falle si Order.Api no está corriendo
	if ($_.Exception.Response.StatusCode -eq 503 -or $_.Exception.InnerException -ne $null) {
		Write-Host "⚠️  Order.Api no está disponible (esto es normal si no está corriendo)" -ForegroundColor Yellow
		Write-Host "  Error: $($_.Exception.InnerException.Message)" -ForegroundColor Gray
	}
	else {
		Write-Host "❌ Error: $_" -ForegroundColor Red
	}
}

# 5. Test Rate Limiting
Write-Host "`n⏱️  5. Testing Rate Limiting (multiple rapid requests)..." -ForegroundColor Yellow
$successCount = 0
$limitExceededCount = 0

for ($i = 1; $i -le 5; $i++) {
	try {
		$response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
			-Headers @{ 
				Authorization = "Bearer $token"
				"X-Correlation-ID" = [guid]::NewGuid().ToString()
			} `
			-Method Get `
			-SkipCertificateCheck
		$successCount++
	}
	catch {
		if ($_.Exception.Response.StatusCode -eq 429) {
			$limitExceededCount++
		}
	}
	Start-Sleep -Milliseconds 50
}

Write-Host "  Requests forwarded: $successCount" -ForegroundColor Green
if ($limitExceededCount -gt 0) {
	Write-Host "  Rate limit triggered: $limitExceededCount" -ForegroundColor Yellow
}

Write-Host "`n✅ Tests Complete!" -ForegroundColor Green
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "  - Correlation ID: $correlationId" -ForegroundColor Gray
Write-Host "  - Check logs: logs/gateway-*.txt" -ForegroundColor Gray
