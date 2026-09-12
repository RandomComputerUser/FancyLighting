namespace FancyLighting.Common.Graphics;

internal record struct SpriteBatchParameters(
    SpriteSortMode sortMode,
    BlendState blendState,
    SamplerState samplerState,
    DepthStencilState depthStencilState,
    RasterizerState rasterizerState,
    Effect customEffect,
    Matrix transformMatrix
)
{
    public SpriteBatchParameters(SpriteBatch spriteBatch)
        : this(
            sortMode: SpriteBatchAccessors.sortMode(spriteBatch),
            blendState: SpriteBatchAccessors.blendState(spriteBatch),
            samplerState: SpriteBatchAccessors.samplerState(spriteBatch),
            depthStencilState: SpriteBatchAccessors.depthStencilState(spriteBatch),
            rasterizerState: SpriteBatchAccessors.rasterizerState(spriteBatch),
            customEffect: SpriteBatchAccessors.customEffect(spriteBatch),
            transformMatrix: SpriteBatchAccessors.transformMatrix(spriteBatch)
        ) { }

    public readonly void BeginSpriteBatch(SpriteBatch spriteBatch) =>
        spriteBatch.Begin(
            sortMode,
            blendState,
            samplerState,
            depthStencilState,
            rasterizerState,
            customEffect,
            transformMatrix
        );
}
