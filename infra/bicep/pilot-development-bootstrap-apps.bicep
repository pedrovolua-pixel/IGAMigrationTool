// Preparation only. Paid recreation needs a fresh priced session and cleanup approval.
// All platform resources and distinct image-pull identities must already exist.
@minLength(1)
param environmentName string
@minLength(5)
@maxLength(50)
param registryName string
@minLength(1)
param webIdentityName string
@minLength(1)
param workerIdentityName string
@minLength(2)
@maxLength(32)
param webAppName string
@minLength(2)
@maxLength(32)
param workerAppName string
@allowed(['iga/azure-development-bootstrap'])
param imageRepository string
@minLength(64)
@maxLength(64)
param imageDigest string

module bootstrap 'modules/pilot-development-bootstrap-apps.bicep' = {
  name: 'development-bootstrap-apps'
  params: {
    environmentName: environmentName
    registryName: registryName
    webIdentityName: webIdentityName
    workerIdentityName: workerIdentityName
    webAppName: webAppName
    workerAppName: workerAppName
    imageRepository: imageRepository
    imageDigest: imageDigest
  }
}
