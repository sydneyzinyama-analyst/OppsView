# OppsView CV matcher container (Python + FastAPI).

FROM python:3.13-slim AS build
WORKDIR /src
COPY pyproject.toml ./
COPY src/ src/
# Build a wheel of the matcher plus wheels of its dependencies.
RUN pip wheel --no-cache-dir --wheel-dir /wheels .

FROM python:3.13-slim AS runtime
# Don't write .pyc files into the image; print logs straight away.
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
COPY --from=build /wheels /wheels
RUN pip install --no-cache-dir /wheels/* && rm -rf /wheels \
    && useradd --create-home --uid 10001 matcher
USER matcher
EXPOSE 8000
# 0.0.0.0: listen on the container's network, not just inside it.
CMD ["uvicorn", "matcher.api:app", "--host", "0.0.0.0", "--port", "8000"]
