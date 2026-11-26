import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
}

android {
    namespace = "de.ratio.backup"
    compileSdk = 36

    defaultConfig {
        applicationId = "de.ratio.backup"
        minSdk = 31
        targetSdk = 36
        versionCode = (System.getenv("RB_VERSION_CODE") ?: "1").toInt()
        versionName = System.getenv("RB_VERSION_NAME") ?: "0.0-lokal"
    }

    // Signaturschlüssel: liegt verschlüsselt im Repo (keystore/release.jks.enc), die CI entschlüsselt ihn
    // mit dem Secret KEYSTORE_PASSWORD. Lokal ohne Schlüssel wird mit dem Debug-Schlüssel signiert.
    val ks = System.getenv("RB_KEYSTORE")
    signingConfigs {
        create("ratio") {
            if (ks != null) {
                storeFile = file(ks)
                storePassword = System.getenv("RB_KEYSTORE_PASSWORD")
                keyAlias = "ratiobackup"
                keyPassword = System.getenv("RB_KEYSTORE_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = if (ks != null) signingConfigs.getByName("ratio") else signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.material3)
    implementation(libs.kotlinx.coroutines.android)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.okhttp)

    testImplementation(libs.junit)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.kotlinx.coroutines.test)
}
