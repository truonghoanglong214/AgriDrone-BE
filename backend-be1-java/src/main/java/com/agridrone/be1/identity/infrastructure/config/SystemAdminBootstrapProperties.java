package com.agridrone.be1.identity.infrastructure.config;

import jakarta.validation.constraints.AssertTrue;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.Size;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@Validated
@ConfigurationProperties(prefix = "agridrone.identity.system-admin-bootstrap")
public class SystemAdminBootstrapProperties {
    private boolean enabled;

    @Email(message = "System Admin bootstrap email is invalid")
    @Size(max = 320, message = "System Admin bootstrap email must not exceed 320 characters")
    private String email = "";

    @Size(max = 150, message = "System Admin bootstrap full name must not exceed 150 characters")
    private String fullName = "System Administrator";

    public boolean isEnabled() {
        return enabled;
    }

    public void setEnabled(boolean enabled) {
        this.enabled = enabled;
    }

    public String getEmail() {
        return email;
    }

    public void setEmail(String email) {
        this.email = email == null ? "" : email;
    }

    public String getFullName() {
        return fullName;
    }

    public void setFullName(String fullName) {
        this.fullName = fullName == null ? "" : fullName;
    }

    @AssertTrue(message = "System Admin bootstrap email is required when bootstrap is enabled")
    public boolean isEmailConfiguredWhenEnabled() {
        return !enabled || !email.isBlank();
    }

    @AssertTrue(message = "System Admin bootstrap full name is required when bootstrap is enabled")
    public boolean isFullNameConfiguredWhenEnabled() {
        return !enabled || !fullName.isBlank();
    }
}
