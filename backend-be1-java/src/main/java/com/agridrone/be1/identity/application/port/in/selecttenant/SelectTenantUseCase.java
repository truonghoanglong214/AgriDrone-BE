package com.agridrone.be1.identity.application.port.in.selecttenant;

import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import java.util.UUID;

public interface SelectTenantUseCase {
    LoginUserResult select(SelectTenantCommand command);

    record SelectTenantCommand(String selectionToken, UUID tenantId) {
    }
}
