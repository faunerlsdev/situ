keytool -printcert -jarfile build-1793317957088.aab | grep -E "Owner|SHA1|SHA256|Valid"

keytool -list -v -keystore @faunerls.dev__situ.jks | grep -E "Alias name|Owner|SHA1|SHA256|Valid"
cd apps/mobile
eas credentials --platform android