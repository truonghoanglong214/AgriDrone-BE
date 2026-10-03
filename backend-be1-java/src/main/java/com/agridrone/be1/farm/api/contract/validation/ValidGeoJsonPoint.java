package com.agridrone.be1.farm.api.contract.validation;

import jakarta.validation.Constraint;
import jakarta.validation.Payload;
import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@Documented
@Constraint(validatedBy = GeoJsonPointValidator.class)
@Target({ElementType.TYPE, ElementType.ANNOTATION_TYPE})
@Retention(RetentionPolicy.RUNTIME)
public @interface ValidGeoJsonPoint {
    String message() default
            "Point must contain valid longitude and latitude coordinates.";

    Class<?>[] groups() default {};

    Class<? extends Payload>[] payload() default {};
}
