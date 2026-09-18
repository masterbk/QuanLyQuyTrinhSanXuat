import java.io.FileInputStream
import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Thông báo đẩy (FCM): plugin google-services CHỈ áp dụng khi có file cấu hình thật (không commit,
// xem android/.gitignore) - để build vẫn chạy được ở máy/CI chưa thiết lập Firebase.
if (file("google-services.json").exists()) {
    apply(plugin = "com.google.gms.google-services")
}

// Khoá ký bản phát hành đọc từ android/key.properties (máy dev tự tạo; CI tạo từ GitHub Secrets - xem
// .github/workflows/mobile-android.yml). File này và file .jks bị .gitignore, KHÔNG commit.
// Không có thì ký bằng khoá debug: chỉ để thử, không phát cho người dùng vì mỗi máy build một khoá khác nhau.
val keystoreProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) FileInputStream(file).use { load(it) }
}
val coKhoaKy = keystoreProperties.isNotEmpty()

android {
    namespace = "vn.tuannghia.hcp_mobile"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId = "vn.tuannghia.hcp_mobile"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (coKhoaKy) {
            create("release") {
                keyAlias = keystoreProperties["keyAlias"] as String
                keyPassword = keystoreProperties["keyPassword"] as String
                storeFile = file(keystoreProperties["storeFile"] as String)
                storePassword = keystoreProperties["storePassword"] as String
            }
        }
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.getByName(if (coKhoaKy) "release" else "debug")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
