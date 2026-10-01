package com.agridrone.be1.shared.auth;

import java.util.ArrayList;
import java.util.Collection;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import org.springframework.core.convert.converter.Converter;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtGrantedAuthoritiesConverter;

/**
 * Maps the cross-language system_role claim to Spring authorities without
 * changing the role value. Scope authorities remain supported for infrastructure
 * clients that use OAuth2 scopes.
 */
public final class JwtAuthoritiesConverter
        implements Converter<Jwt, Collection<GrantedAuthority>> {
    private static final String SYSTEM_ROLE_CLAIM = "system_role";

    private final JwtGrantedAuthoritiesConverter scopeConverter =
            new JwtGrantedAuthoritiesConverter();

    @Override
    public Collection<GrantedAuthority> convert(Jwt jwt) {
        Set<GrantedAuthority> authorities = new LinkedHashSet<>();
        authorities.addAll(scopeConverter.convert(jwt));

        Object rawRoles = jwt.getClaims().get(SYSTEM_ROLE_CLAIM);
        for (String role : values(rawRoles)) {
            authorities.add(new SimpleGrantedAuthority(role));
        }
        return List.copyOf(authorities);
    }

    private static Collection<String> values(Object raw) {
        if (raw == null) {
            return List.of();
        }
        if (raw instanceof Collection<?> collection) {
            List<String> values = new ArrayList<>();
            for (Object value : collection) {
                if (value != null && !value.toString().isBlank()) {
                    values.add(value.toString());
                }
            }
            return values;
        }
        String value = raw.toString();
        return value.isBlank() ? List.of() : List.of(value);
    }
}
